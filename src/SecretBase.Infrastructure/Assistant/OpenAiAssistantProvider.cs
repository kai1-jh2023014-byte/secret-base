using System.Net.Http.Headers;
using System.Text;
using SecretBase.Core.Assistant;

namespace SecretBase.Infrastructure.Assistant;

/// <summary>OpenAI Chat Completions. No SDK. Never logs the API key.</summary>
public sealed class OpenAiAssistantProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly Func<string?> _getApiKey;

    public OpenAiAssistantProvider(HttpClient http, Func<string?> getApiKey)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _getApiKey = getApiKey ?? throw new ArgumentNullException(nameof(getApiKey));
    }

    public string ProviderId => AssistantProviderIds.OpenAi;

    public string DisplayName => "OpenAI";

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

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(
            OpenAiChatCompletionSerializer.BuildBody(messages, tools, model),
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

        return OpenAiChatCompletionSerializer.ParseCompletion(body);
    }
}
