namespace SecretBase.Core.Assistant;

/// <summary>Builds user-facing AI failure copy with enough detail to diagnose.</summary>
public static class AssistantErrorDetail
{
    public const int DefaultHttpTimeoutSeconds = 60;

    public const int FollowUpTimeoutSeconds = 15;

    public static string Timeout(
        string? providerDisplayName,
        string? providerId,
        string? model,
        int timeoutSeconds,
        string phase)
    {
        var provider = string.IsNullOrWhiteSpace(providerDisplayName)
            ? (string.IsNullOrWhiteSpace(providerId) ? "AI provider" : providerId)
            : providerDisplayName.Trim();
        var modelPart = string.IsNullOrWhiteSpace(model) ? "default model" : model.Trim();
        var phasePart = string.IsNullOrWhiteSpace(phase) ? "response" : phase.Trim();
        return
            $"{AssistantUserMessages.Timeout} "
            + $"Provider: {provider} ({providerId ?? "unknown"}), model: {modelPart}. "
            + $"No {phasePart} within {timeoutSeconds}s. "
            + "Check network / API key in AI Settings, or switch to Local (Ollama). "
            + "Timed Japanese schedules (e.g. 19時… / 22:00から…) and Pomodoro can run without the remote model.";
    }

    public static string Network(
        string? providerDisplayName,
        string? providerId,
        string? detail = null)
    {
        var provider = string.IsNullOrWhiteSpace(providerDisplayName)
            ? (string.IsNullOrWhiteSpace(providerId) ? "AI provider" : providerId)
            : providerDisplayName.Trim();
        var extra = string.IsNullOrWhiteSpace(detail) ? string.Empty : " " + detail.Trim();
        return
            $"{AssistantUserMessages.NetworkError} "
            + $"Provider: {provider} ({providerId ?? "unknown"}).{extra} "
            + "Check internet connectivity and firewall, then retry. "
            + AssistantUserMessages.OpenSettings;
    }

    public static string Unavailable(
        string? providerDisplayName,
        string? providerId,
        string? detail = null)
    {
        var provider = string.IsNullOrWhiteSpace(providerDisplayName)
            ? (string.IsNullOrWhiteSpace(providerId) ? "AI provider" : providerId)
            : providerDisplayName.Trim();
        var extra = string.IsNullOrWhiteSpace(detail) ? string.Empty : " Detail: " + detail.Trim();
        return
            $"{AssistantUserMessages.Unavailable} "
            + $"Provider: {provider} ({providerId ?? "unknown"}).{extra} "
            + "If a remote key is set, try Test Connection in AI Settings; otherwise enable Local (Ollama).";
    }

    public static string NotConfigured(string? providerDisplayName, string? providerId) =>
        $"{AssistantUserMessages.NotConfigured} "
        + $"Active provider would be {providerDisplayName ?? providerId ?? "AI"}. "
        + AssistantUserMessages.OpenSettings
        + " Save an OpenAI or Gemini key (separate slots), or choose Local.";

    /// <summary>True when the message is (or starts with) the short timeout banner.</summary>
    public static bool IsTimeoutMessage(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && (string.Equals(message, AssistantUserMessages.Timeout, StringComparison.Ordinal)
            || message.StartsWith(AssistantUserMessages.Timeout, StringComparison.Ordinal)
            || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase));
}
