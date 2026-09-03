using System.Net;
using System.Net.Http;
using SecretBase.Core.Connectors;

namespace SecretBase.Infrastructure.Integration;

/// <summary>
/// HTTP transport for declared endpoints only. Redirects are refused. Certificates stay validated.
/// </summary>
public sealed class HttpIntegrationTransport : IIntegrationTransport
{
    private readonly HttpMessageHandler _handler;

    public HttpIntegrationTransport(HttpMessageHandler? handler = null)
    {
        _handler = handler ?? new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CheckCertificateRevocationList = true,
            AutomaticDecompression = DecompressionMethods.None
        };
    }

    public async Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HttpEndpointPolicy.IsLoopbackHost(request.Url.Host)
            && request.Url.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return new TransportResponse(false, 0, string.Empty, "denied-scheme", "Cleartext HTTP is not allowed for this host.");
        }

        using var client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = request.Timeout
        };
        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
        foreach (var header in IntegrationSecretSanitizer.RedactHeaders(request.Headers))
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var header in request.Headers)
        {
            if (!message.Headers.Contains(header.Key))
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (request.Body is not null)
        {
            message.Content = new StringContent(request.Body, System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            return new TransportResponse(false, 0, string.Empty, "timeout", "Request timed out.", TimedOut: true);
        }
        catch (HttpRequestException)
        {
            return new TransportResponse(false, 0, string.Empty, "unavailable", "Connector unavailable.");
        }

        using (response)
        {
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                return new TransportResponse(false, (int)response.StatusCode, string.Empty, "redirect", "Redirects are not followed.", Redirected: true);
            }

            if (response.Content.Headers.ContentLength is > 0
                && response.Content.Headers.ContentLength > request.MaxResponseBytes)
            {
                return new TransportResponse(false, (int)response.StatusCode, string.Empty, "oversized", "Response exceeded the size limit.", Oversized: true);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var limited = new MemoryStream();
            var buffer = new byte[4096];
            var total = 0;
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                total += read;
                if (total > request.MaxResponseBytes)
                {
                    return new TransportResponse(false, (int)response.StatusCode, string.Empty, "oversized", "Response exceeded the size limit.", Oversized: true);
                }

                limited.Write(buffer, 0, read);
            }

            var body = System.Text.Encoding.UTF8.GetString(limited.ToArray());
            if (!response.IsSuccessStatusCode)
            {
                return new TransportResponse(
                    false,
                    (int)response.StatusCode,
                    string.Empty,
                    response.StatusCode == HttpStatusCode.Unauthorized ? "auth" : "unavailable",
                    "Connector unavailable.");
            }

            return new TransportResponse(true, (int)response.StatusCode, IntegrationSecretSanitizer.Redact(body), null, null);
        }
    }
}

public sealed class SecureIntegrationSecretResolver : IIntegrationSecretResolver
{
    private readonly SecretBase.Platform.Abstractions.ISecureSecretStore _store;

    public SecureIntegrationSecretResolver(SecretBase.Platform.Abstractions.ISecureSecretStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public bool TryResolveHeader(string? credentialReference, string? headerName, out string? headerValue)
    {
        headerValue = null;
        if (string.IsNullOrWhiteSpace(credentialReference))
        {
            return false;
        }

        var key = CredentialReference.Normalize(credentialReference);
        if (!_store.TryGetSecret(key, out var secret) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var name = string.IsNullOrWhiteSpace(headerName) ? "Authorization" : headerName;
        headerValue = name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && !secret.Contains(' ', StringComparison.Ordinal)
            ? "Bearer " + secret
            : secret;
        return true;
    }
}

/// <summary>Prefers real HTTP, falls back to loopback canned responses for demo hosts.</summary>
public sealed class CompositeIntegrationTransport : IIntegrationTransport
{
    private readonly HttpIntegrationTransport _http;
    private readonly LoopbackIntegrationTransport _loopback = new();

    public CompositeIntegrationTransport(HttpMessageHandler? handler = null)
    {
        _http = new HttpIntegrationTransport(handler);
    }

    public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken = default)
    {
        if (HttpEndpointPolicy.IsLoopbackHost(request.Url.Host)
            && (request.Url.Port is 3847 or 3848))
        {
            return _loopback.SendAsync(request, cancellationToken);
        }

        return _http.SendAsync(request, cancellationToken);
    }
}
