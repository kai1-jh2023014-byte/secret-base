namespace SecretBase.Core.Themes;

/// <summary>Quiet product copy for empty, error, and next-step states. Never "No data".</summary>
public static class UxCopy
{
    public const string MemoryEmpty =
        "Nothing remembered yet.\nCapture an idea, a todo, or a note — Secret Base will keep it here.";

    public const string AutomationEmpty =
        "No quiet rules yet.\nSecret Base stays silent until you allow a suggestion.";

    public const string IntegrationsEmpty =
        "No apps connected yet.\nPaste a secretbase.integration.json to let Secret Base see a named capability.";

    public const string WorkspaceEmpty =
        "Nothing prepared yet.\nYour next project can start here — Continue still asks before opening anything.";

    public const string TasksEmpty =
        "No open tasks.\nAdd one when something needs you.";

    public const string CalendarQuiet =
        "No events on the calendar today.\nLocal workspace is still here.";

    public const string CapturePrompt = "What do you want to remember?";

    public const string CapturePlaceholder = "An idea, a todo, a note…";

    public const string CaptureRefused =
        "That capture was not saved.\nEmpty text and secret-like strings are refused.";

    public const string PaletteHeading = "Search anything";

    public const string PalettePlaceholder = "Projects, apps, calendar, tasks, memory…";

    public const string PaletteHint = "↑↓ to move · Enter to run · Esc to close · Continue still confirms";

    public const string OnboardingTitle = "Welcome to Secret Base";

    public const string OnboardingBody =
        "A personal space for your work, ideas, projects, and AI.\nQuiet by default. Choose a style — you can change it later.";

    public const string CalendarUnavailable =
        "Calendar isn't available right now.\nYour local workspace is still here.";

    public const string AiUnavailable =
        "Base AI isn't reachable right now.\nCommand Center, capture, and your workspace still work.";

    public static string FriendlyAiError(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || raw.Equals("AI provider is unavailable.", StringComparison.Ordinal)
            || raw.StartsWith("Could not connect to AI", StringComparison.OrdinalIgnoreCase))
        {
            return AiUnavailable;
        }

        return raw.Trim();
    }

    public const string Retry = "Retry";

    public static string FirstLine(string copy)
    {
        var n = copy.IndexOf('\n');
        return n < 0 ? copy : copy[..n];
    }

    public static string IntegrationUnavailable(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? "This integration isn't available right now.\nYour Base is otherwise unchanged."
            : $"{name} isn't available right now.\nYour Base is otherwise unchanged.";
}
