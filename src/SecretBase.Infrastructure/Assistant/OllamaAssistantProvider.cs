using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Assistant;

namespace SecretBase.Infrastructure.Assistant;

/// <summary>
/// Ollama via OpenAI-compatible <c>/v1/chat/completions</c>. No API key required.
/// Tool calling depends on the selected model.
/// </summary>
public sealed class OllamaAssistantProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly Func<string> _getBaseUrl;
    private readonly Func<string> _getModel;

    public OllamaAssistantProvider(HttpClient http, Func<string> getBaseUrl, Func<string> getModel)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _getBaseUrl = getBaseUrl ?? throw new ArgumentNullException(nameof(getBaseUrl));
        _getModel = getModel ?? throw new ArgumentNullException(nameof(getModel));
    }

    public string ProviderId => AssistantProviderIds.Local;

    public string DisplayName => "Local AI";

    public async Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeBaseUrl(_getBaseUrl());
        var resolvedModel = string.IsNullOrWhiteSpace(model) ? _getModel() : model;
        if (string.IsNullOrWhiteSpace(resolvedModel))
        {
            resolvedModel = AssistantSettings.DefaultLocalModel;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/v1/chat/completions");
        request.Content = new StringContent(
            OpenAiChatCompletionSerializer.BuildBody(messages, tools, resolvedModel),
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
            return AiProviderResponse.Unavailable(
                "Local AI is not running. Start Ollama or check the endpoint in AI Settings.");
        }
        catch (Exception)
        {
            return AiProviderResponse.Unavailable(AssistantUserMessages.Unavailable);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadGateway
                ? AiProviderResponse.Unavailable(
                    "Local AI is not running. Start Ollama or check the endpoint in AI Settings.")
                : OpenAiChatCompletionSerializer.MapHttpError(response.StatusCode, body);
        }

        return OpenAiChatCompletionSerializer.ParseCompletion(body);
    }

    internal static string NormalizeBaseUrl(string raw)
    {
        var trimmed = (raw ?? string.Empty).Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(trimmed)
            ? AssistantSettings.DefaultLocalBaseUrl.TrimEnd('/')
            : trimmed;
    }
}
