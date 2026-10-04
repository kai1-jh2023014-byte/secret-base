using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Jev;

namespace SecretBase.Infrastructure.Jev;

/// <summary>
/// POST to the documented Jev decide endpoint. Bearer auth only.
/// Does not start processes, open files, or log the API key.
/// </summary>
public sealed class JevApiClient : IJevDecisionClient
{
    private readonly HttpClient _http;

    public JevApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<JevClientResult> DecideAsync(
        string apiKey,
        string requestJson,
        CancellationToken cancellationToken = default)
    {
        var key = apiKey?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return JevClientResult.Fail(JevClientFailure.NotConfigured, JevUserMessages.NotConfigured);
        }

        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return JevClientResult.Fail(JevClientFailure.InvalidRequest, JevUserMessages.InvalidResponse);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, JevDecisionContract.ResolveEndpoint(key));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return JevClientResult.Fail(JevClientFailure.Timeout, JevUserMessages.Timeout);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return JevClientResult.Fail(JevClientFailure.Network, JevUserMessages.NetworkError);
        }
        catch (Exception)
        {
            return JevClientResult.Fail(JevClientFailure.Unavailable, JevUserMessages.Unavailable);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return JevClientResult.Ok(body);
            }

            return MapStatus((int)response.StatusCode, body, key);
        }
    }

    internal static JevClientResult MapStatus(int statusCode, string? body, string apiKey)
    {
        var detail = ReadPublicError(body, apiKey);
        return statusCode switch
        {
            (int)HttpStatusCode.BadRequest => JevClientResult.Fail(
                JevClientFailure.InvalidRequest,
                detail ?? "Jev rejected the decision request.",
                statusCode),
            (int)HttpStatusCode.Unauthorized => JevClientResult.Fail(
                JevClientFailure.Authentication,
                JevUserMessages.AuthenticationFailed,
                statusCode),
            (int)HttpStatusCode.PaymentRequired => JevClientResult.Fail(
                JevClientFailure.PaymentRequired,
                JevUserMessages.InsufficientCredits,
                statusCode),
            (int)HttpStatusCode.Forbidden => JevClientResult.Fail(
                JevClientFailure.Forbidden,
                JevUserMessages.Inactive,
                statusCode),
            (int)HttpStatusCode.NotFound => JevClientResult.Fail(
                JevClientFailure.InvalidRequest,
                "Jev endpoint was not found.",
                statusCode),
            (int)HttpStatusCode.BadGateway => JevClientResult.Fail(
                JevClientFailure.Unavailable,
                JevUserMessages.Unavailable,
                statusCode),
            _ => JevClientResult.Fail(
                JevClientFailure.Unavailable,
                detail ?? JevUserMessages.Unavailable,
                statusCode)
        };
    }

    internal static string? ReadPublicError(string? body, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        string? text = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            {
                text = error.GetString();
            }
            else if (doc.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String)
            {
                text = code.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            text = text.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
        }

        text = text.Replace("Bearer ", string.Empty, StringComparison.Ordinal);
        return text.Length <= 160 ? text : text[..160];
    }
}
