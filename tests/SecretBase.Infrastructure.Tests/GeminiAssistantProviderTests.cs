using SecretBase.Core.Assistant;
using SecretBase.Infrastructure.Assistant;

namespace SecretBase.Infrastructure.Tests;

public class GeminiAssistantProviderTests
{
    [Theory]
    [InlineData(null, AssistantSettings.DefaultGeminiModel)]
    [InlineData("", AssistantSettings.DefaultGeminiModel)]
    [InlineData("gpt-4o-mini", AssistantSettings.DefaultGeminiModel)]
    [InlineData("llama3.2", AssistantSettings.DefaultGeminiModel)]
    [InlineData("gemini-2.0-flash", "gemini-2.0-flash")]
    public void ResolveModel_ReplacesOpenAiStyleNames(string? input, string expected)
    {
        Assert.Equal(expected, GeminiAssistantProvider.ResolveModel(input));
    }

    [Fact]
    public void BuildBody_IncludesSystemInstructionAndTools()
    {
        var body = GeminiAssistantProvider.BuildBody(
            [
                new AiMessage { Role = AiMessageRole.System, Content = "You are Secret Base." },
                new AiMessage { Role = AiMessageRole.User, Content = "What is next?" }
            ],
            [
                new AssistantToolDefinition
                {
                    Name = "calendar_get_today",
                    Description = "Today's events",
                    Parameters =
                    [
                        new AssistantToolParameter
                        {
                            Name = "limit",
                            Type = "integer",
                            Description = "Max events",
                            Required = false
                        }
                    ]
                }
            ]);

        Assert.Contains("system_instruction", body, StringComparison.Ordinal);
        Assert.Contains("You are Secret Base.", body, StringComparison.Ordinal);
        Assert.Contains("function_declarations", body, StringComparison.Ordinal);
        Assert.Contains("calendar_get_today", body, StringComparison.Ordinal);
        Assert.Contains("\"role\":\"user\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseResponse_ReadsTextAndFunctionCalls()
    {
        var json =
            """
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      { "text": "Checking calendar." },
                      { "functionCall": { "name": "calendar_get_today", "args": { "limit": 3 } } }
                    ]
                  }
                }
              ]
            }
            """;

        var response = GeminiAssistantProvider.ParseResponse(json);
        Assert.Equal(AiProviderStatus.Ok, response.Status);
        Assert.Contains("Checking calendar.", response.Content, StringComparison.Ordinal);
        Assert.Single(response.ToolCalls);
        Assert.Equal("calendar_get_today", response.ToolCalls[0].Name);
        Assert.Contains("limit", response.ToolCalls[0].ArgumentsJson, StringComparison.Ordinal);
    }
}
