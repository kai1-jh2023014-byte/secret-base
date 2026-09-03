using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Assistant;

namespace SecretBase.Infrastructure.Assistant;

/// <summary>Google Gemini generateContent API. Requires user API key in secure storage.</summary>
public sealed class GeminiAssistantProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly Func<string?> _getApiKey;

    public GeminiAssistantProvider(HttpClient http, Func<string?> getApiKey)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _getApiKey = getApiKey ?? throw new ArgumentNullException(nameof(getApiKey));
    }

    public string ProviderId => AssistantProviderIds.Gemini;

    public string DisplayName => "Gemini";

    public async Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default)
    {
        var key = _getApiKey()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return AiProviderResponse.NotConfigured();
        }

        var resolvedModel = string.IsNullOrWhiteSpace(model) ? "gemini-2.0-flash" : model.Trim();
        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(resolvedModel)}:generateContent?key={Uri.EscapeDataString(key)}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(
            BuildBody(messages, tools),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return AiProviderResponse.Unavailable(AssistantUserMessages.NetworkError);
        }
        catch (Exception)
        {
            return AiProviderResponse.Unavailable(AssistantUserMessages.Unavailable);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return OpenAiChatCompletionSerializer.MapHttpError(response.StatusCode, body);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();
            return AiProviderResponse.Text(text ?? string.Empty);
        }
        catch (Exception)
        {
            return AiProviderResponse.Fail("Gemini returned an invalid response.");
        }
    }

    private static string BuildBody(IReadOnlyList<AiMessage> messages, IReadOnlyList<AssistantToolDefinition> tools)
    {
        _ = tools;
        var contents = messages
            .Where(m => m.Role is AiMessageRole.User or AiMessageRole.Assistant)
            .Select(m => new Dictionary<string, object?>
            {
                ["role"] = m.Role == AiMessageRole.User ? "user" : "model",
                ["parts"] = new[] { new Dictionary<string, object?> { ["text"] = m.Content ?? string.Empty } }
            })
            .ToList();

        return JsonSerializer.Serialize(new Dictionary<string, object?> { ["contents"] = contents });
    }
}
