using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
            BuildBody(messages, tools, model),
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
            return MapHttpError(response.StatusCode, body);
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var choice = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
            var content = choice.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : null;
            var calls = ParseToolCalls(choice);
            return calls.Count > 0
                ? AiProviderResponse.Tools(calls, content)
                : AiProviderResponse.Text(content ?? string.Empty);
        }
        catch (Exception)
        {
            return AiProviderResponse.Fail("AI provider returned an invalid response.");
        }
    }

    private static string BuildBody(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? AssistantSettings.DefaultOpenAiModel : model,
            ["messages"] = messages.Select(ToOpenAiMessage).ToList()
        };
        if (tools.Count > 0)
        {
            payload["tools"] = tools.Select(ToOpenAiTool).ToList();
        }

        return JsonSerializer.Serialize(payload);
    }

    private static Dictionary<string, object?> ToOpenAiMessage(AiMessage message)
    {
        var role = message.Role switch
        {
            AiMessageRole.System => "system",
            AiMessageRole.User => "user",
            AiMessageRole.Tool => "tool",
            _ => "assistant"
        };

        var dict = new Dictionary<string, object?>
        {
            ["role"] = role
        };
        if (message.Role == AiMessageRole.Assistant && message.ToolCalls.Count > 0)
        {
            dict["content"] = string.IsNullOrWhiteSpace(message.Content) ? null : message.Content;
        }
        else
        {
            dict["content"] = message.Content ?? string.Empty;
        }
        if (message.Role == AiMessageRole.Tool && !string.IsNullOrWhiteSpace(message.ToolCallId))
        {
            dict["tool_call_id"] = message.ToolCallId;
        }

        if (message.Role == AiMessageRole.Assistant && message.ToolCalls.Count > 0)
        {
            dict["tool_calls"] = message.ToolCalls.Select(c => new Dictionary<string, object?>
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new Dictionary<string, object?>
                {
                    ["name"] = c.Name,
                    ["arguments"] = c.ArgumentsJson
                }
            }).ToList();
        }

        return dict;
    }

    private static Dictionary<string, object?> ToOpenAiTool(AssistantToolDefinition tool)
    {
        var properties = new Dictionary<string, object?>();
        var required = new List<string>();
        foreach (var p in tool.Parameters)
        {
            properties[p.Name] = new Dictionary<string, object?>
            {
                ["type"] = p.Type,
                ["description"] = p.Description
            };
            if (p.Required)
            {
                required.Add(p.Name);
            }
        }

        var parameters = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0)
        {
            parameters["required"] = required;
        }

        return new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = new Dictionary<string, object?>
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["parameters"] = parameters
            }
        };
    }

    private static IReadOnlyList<AiToolCall> ParseToolCalls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AiToolCall>();
        }

        var list = new List<AiToolCall>();
        foreach (var el in arr.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (!el.TryGetProperty("function", out var fn))
            {
                continue;
            }

            var name = fn.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
            var args = fn.TryGetProperty("arguments", out var argsEl) ? argsEl.GetString() : "{}";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            list.Add(new AiToolCall { Id = id!, Name = name!, ArgumentsJson = args ?? "{}" });
        }

        return list;
    }

    private static AiProviderResponse MapHttpError(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return AiProviderResponse.Fail(AssistantUserMessages.AuthenticationFailed);
        }

        if ((int)status == 429)
        {
            return AiProviderResponse.Fail(AssistantUserMessages.RateLimitReached);
        }

        if ((int)status >= 500)
        {
            return AiProviderResponse.Unavailable(AssistantUserMessages.NetworkError);
        }

        if (body.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase))
        {
            return AiProviderResponse.Fail(AssistantUserMessages.AuthenticationFailed);
        }

        if (body.Contains("rate_limit", StringComparison.OrdinalIgnoreCase))
        {
            return AiProviderResponse.Fail(AssistantUserMessages.RateLimitReached);
        }

        return AiProviderResponse.Fail(AssistantUserMessages.Unavailable);
    }
}
