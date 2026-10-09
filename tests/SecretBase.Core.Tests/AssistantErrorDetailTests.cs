using SecretBase.Core.Assistant;

namespace SecretBase.Core.Tests;

public class AssistantErrorDetailTests
{
    [Fact]
    public void Timeout_IncludesProviderModelAndHint()
    {
        var message = AssistantErrorDetail.Timeout("Gemini", "gemini", "gemini-2.0-flash", 60, "first reply");
        Assert.StartsWith(AssistantUserMessages.Timeout, message);
        Assert.Contains("Provider: Gemini (gemini)", message, StringComparison.Ordinal);
        Assert.Contains("gemini-2.0-flash", message, StringComparison.Ordinal);
        Assert.Contains("60s", message, StringComparison.Ordinal);
        Assert.Contains("AI Settings", message, StringComparison.Ordinal);
        Assert.Contains("Ollama", message, StringComparison.Ordinal);
    }
}
