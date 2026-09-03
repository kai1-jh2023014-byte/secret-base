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

        if (string.Equals(status.ProviderId, AssistantProviderIds.Local, StringComparison.OrdinalIgnoreCase))
        {
            return "Base AI ● Local";
        }

        if (status.IsConfigured)
        {
            return "Base AI ● Online";
        }

        // Remote not configured → resilient provider uses Local.
        return "Base AI ● Local";
    }

    public static string FormatShort(AssistantProviderStatusInfo? status)
    {
        var line = Format(status);
        return line.Replace("Base AI ", string.Empty, StringComparison.Ordinal);
    }
}
