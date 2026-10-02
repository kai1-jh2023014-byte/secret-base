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

        var resolvedModel = ResolveModel(model);
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

        return ParseResponse(body);
    }

    internal static string ResolveModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)
            || model.Contains("gpt-", StringComparison.OrdinalIgnoreCase)
            || model.Contains("o1", StringComparison.OrdinalIgnoreCase)
            || model.Contains("o3", StringComparison.OrdinalIgnoreCase)
            || model.Contains("claude", StringComparison.OrdinalIgnoreCase)
            || model.Contains("llama", StringComparison.OrdinalIgnoreCase))
        {
            return AssistantSettings.DefaultGeminiModel;
        }

        return model.Trim();
    }

    internal static string BuildBody(IReadOnlyList<AiMessage> messages, IReadOnlyList<AssistantToolDefinition> tools)
    {
        var systemText = string.Join(
            "\n\n",
            messages
                .Where(m => m.Role == AiMessageRole.System && !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => m.Content!.Trim()));

        var contents = new List<Dictionary<string, object?>>();
        foreach (var message in messages)
        {
            if (message.Role == AiMessageRole.System)
            {
                continue;
            }

            if (message.Role == AiMessageRole.User)
            {
                contents.Add(new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = message.Content ?? string.Empty }
                    }
                });
                continue;
            }

            if (message.Role == AiMessageRole.Assistant)
            {
                var parts = new List<object>();
                if (!string.IsNullOrWhiteSpace(message.Content))
                {
                    parts.Add(new Dictionary<string, object?> { ["text"] = message.Content });
                }

                foreach (var call in message.ToolCalls)
                {
                    parts.Add(new Dictionary<string, object?>
                    {
                        ["functionCall"] = new Dictionary<string, object?>
                        {
                            ["name"] = call.Name,
                            ["args"] = ParseArgsObject(call.ArgumentsJson)
                        }
                    });
                }

                if (parts.Count == 0)
                {
                    parts.Add(new Dictionary<string, object?> { ["text"] = string.Empty });
                }

                contents.Add(new Dictionary<string, object?>
                {
                    ["role"] = "model",
                    ["parts"] = parts
                });
                continue;
            }

            if (message.Role == AiMessageRole.Tool)
            {
                contents.Add(new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["functionResponse"] = new Dictionary<string, object?>
                            {
                                ["name"] = ResolveToolName(message),
                                ["response"] = new Dictionary<string, object?>
                                {
                                    ["result"] = message.Content ?? string.Empty
                                }
                            }
                        }
                    }
                });
            }
        }

        var payload = new Dictionary<string, object?> { ["contents"] = contents };
        if (!string.IsNullOrWhiteSpace(systemText))
        {
            payload["system_instruction"] = new Dictionary<string, object?>
            {
                ["parts"] = new object[]
                {
                    new Dictionary<string, object?> { ["text"] = systemText }
                }
            };
        }

        if (tools.Count > 0)
        {
            payload["tools"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["function_declarations"] = tools.Select(ToGeminiTool).ToList()
                }
            };
        }

        return JsonSerializer.Serialize(payload);
    }

    internal static AiProviderResponse ParseResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.GetArrayLength() == 0)
            {
                return AiProviderResponse.Fail("Gemini returned an empty response.");
            }

            var content = candidates[0].GetProperty("content");
            if (!content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
            {
                return AiProviderResponse.Fail("Gemini returned an invalid response.");
            }

            var text = new StringBuilder();
            var toolCalls = new List<AiToolCall>();
            var index = 0;
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textNode) && textNode.ValueKind == JsonValueKind.String)
                {
                    text.Append(textNode.GetString());
                }

                if (part.TryGetProperty("functionCall", out var call)
                    && call.TryGetProperty("name", out var nameNode))
                {
                    var name = nameNode.GetString() ?? string.Empty;
                    var argsJson = "{}";
                    if (call.TryGetProperty("args", out var argsNode))
                    {
                        argsJson = argsNode.GetRawText();
                    }

                    toolCalls.Add(new AiToolCall
                    {
                        Id = $"gemini_{index++}_{name}",
                        Name = name,
                        ArgumentsJson = argsJson
                    });
                }
            }

            return toolCalls.Count > 0
                ? AiProviderResponse.Tools(toolCalls, text.ToString())
                : AiProviderResponse.Text(text.ToString());
        }
        catch (Exception)
        {
            return AiProviderResponse.Fail("Gemini returned an invalid response.");
        }
    }

    private static Dictionary<string, object?> ToGeminiTool(AssistantToolDefinition tool)
    {
        var properties = new Dictionary<string, object?>();
        var required = new List<string>();
        foreach (var parameter in tool.Parameters)
        {
            properties[parameter.Name] = new Dictionary<string, object?>
            {
                ["type"] = parameter.Type,
                ["description"] = parameter.Description
            };
            if (parameter.Required)
            {
                required.Add(parameter.Name);
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
            ["name"] = tool.Name,
            ["description"] = tool.Description ?? string.Empty,
            ["parameters"] = parameters
        };
    }

    private static object ParseArgsObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            return JsonSerializer.Deserialize<object>(json) ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

    private static string ResolveToolName(AiMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.ToolCallId)
            && message.ToolCallId.Contains('_', StringComparison.Ordinal))
        {
            var parts = message.ToolCallId.Split('_');
            if (parts.Length >= 3)
            {
                return string.Join('_', parts.Skip(2));
            }
        }

        return "tool";
    }
}
