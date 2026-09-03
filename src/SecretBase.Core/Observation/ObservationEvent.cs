using SecretBase.Core.Memory;

namespace SecretBase.Core.Observation;

public enum ObservationKind
{
    ApplicationActivated = 0,
    WindowActivated = 1,
    WindowTitleChanged = 2,
    IdleStarted = 3,
    IdleEnded = 4,
    UserActivityResumed = 5,
    SystemStartup = 6,
    SystemResume = 7
}

/// <summary>
/// Privacy-first computer observation. Names only — no paths, keystrokes, clipboard, or file contents.
/// </summary>
public sealed class ObservationEvent
{
    public ObservationKind Kind { get; init; }

    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;

    public string? ApplicationName { get; init; }

    public string? WindowTitle { get; init; }
}

public static class ObservationSanitizer
{
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "iexplore"
    };

    public static string? SafeApplicationName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(processName.Trim());
        if (name.Length == 0 || MemoryPolicy.LooksSensitive(name) || MemoryPolicy.LooksLikePath(name))
        {
            return null;
        }

        return name.Length <= 40 ? name : name[..40];
    }

    public static string? SafeWindowTitle(string? title, string? applicationName, IReadOnlyList<string>? projectNames)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var text = title.Trim();
        if (MemoryPolicy.LooksSensitive(text) || MemoryPolicy.LooksLikePath(text))
        {
            return null;
        }

        if (text.Contains("password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("credential", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cut = text.IndexOf('?');
        if (cut > 0)
        {
            text = text[..cut];
        }

        if (IsBrowser(applicationName) && !ContainsProject(text, projectNames))
        {
            return null;
        }

        return text.Length <= 80 ? text : text[..80];
    }

    public static bool IsBrowser(string? applicationName) =>
        !string.IsNullOrWhiteSpace(applicationName) && BrowserProcesses.Contains(applicationName);

    private static bool ContainsProject(string title, IReadOnlyList<string>? projectNames)
    {
        if (projectNames is null)
        {
            return false;
        }

        return projectNames.Any(name =>
            !string.IsNullOrWhiteSpace(name)
            && title.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
