using System.Net.Http.Json;
using System.Text.Json;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Fetches Progress + Genesis from an HTTPS endpoint that returns the same JSON schema
/// as <see cref="JsonProgressGenesisStore"/>. Falls back to <paramref name="fallback"/> on failure.
/// </summary>
public sealed class HttpProgressGenesisProvider : IProgressGenesisProvider
{
    private readonly HttpClient _http;
    private readonly string _url;
    private readonly IProgressGenesisProvider _fallback;
    private readonly JsonSerializerOptions _options;

    public HttpProgressGenesisProvider(
        string url,
        HttpClient? httpClient = null,
        IProgressGenesisProvider? fallback = null,
        JsonSerializerOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("Remote URL must be an absolute http(s) URI.", nameof(url));
        }

        // Prefer HTTPS; allow http only for local loopback during development.
        if (uri.Scheme == Uri.UriSchemeHttp
            && !uri.IsLoopback
            && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Remote Progress/Genesis URL must use HTTPS (except localhost).", nameof(url));
        }

        _url = uri.AbsoluteUri;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _fallback = fallback ?? new LocalJsonProgressGenesisProvider();
        _options = options ?? SecretBaseJson.CreateOptions();
    }

    public string ProviderId => "http-json";

    public string DisplayName => "HTTP JSON";

    public string SourceKind => ProgressGenesisSourceKinds.Http;

    public string Url => _url;

    public async Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync(_url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var snapshot = await response.Content
                .ReadFromJsonAsync<ProgressGenesisSnapshot>(_options, cancellationToken)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return await _fallback.GetAsync(cancellationToken).ConfigureAwait(false);
            }

            snapshot.SourceKind = ProgressGenesisSourceKinds.Http;
            snapshot.UpdatedAt = snapshot.UpdatedAt == default ? DateTimeOffset.UtcNow : snapshot.UpdatedAt;
            snapshot.Normalize();
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fallback = await _fallback.GetAsync(cancellationToken).ConfigureAwait(false);
            fallback.Normalize();
            return fallback;
        }
    }
}
