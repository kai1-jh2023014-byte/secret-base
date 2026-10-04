namespace SecretBase.Core.Assistant;

/// <summary>Quiet Base AI identity line for Clock / chat chrome.</summary>
public static class BaseAiStatusFormatter
{
    public static string Format(AssistantProviderStatusInfo? status)
    {
        if (status is null)
        {
            return "Base AI ○ Unavailable";
        }

        if (string.Equals(status.ProviderId, AssistantProviderIds.Local, StringComparison.OrdinalIgnoreCase)
            || !status.IsConfigured)
        {
            return "Base AI ● Local";
        }

        var name = string.IsNullOrWhiteSpace(status.DisplayName)
            ? NameFor(status.ProviderId)
            : status.DisplayName.Trim();
        return "Base AI ● " + name;
    }

    private static string NameFor(string? providerId) =>
        AssistantProviderSelection.Normalize(providerId) switch
        {
            AssistantProviderIds.Gemini => "Gemini",
            AssistantProviderIds.OpenAi => "OpenAI",
            AssistantProviderIds.Local => "Local",
            _ => "Online"
        };

    public static string FormatShort(AssistantProviderStatusInfo? status)
    {
        var line = Format(status);
        return line.Replace("Base AI ", string.Empty, StringComparison.Ordinal);
    }
}
