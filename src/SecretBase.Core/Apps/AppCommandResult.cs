namespace SecretBase.Core.Apps;

/// <summary>Result of an AppCommand. Host performs launch after validation.</summary>
public sealed class AppCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public AppCommandKind Kind { get; init; }

    public IReadOnlyList<CustomApp> Apps { get; init; } = Array.Empty<CustomApp>();

    public CustomApp? App { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? CursorFolderPath { get; init; }

    public static AppCommandResult Ok(
        AppCommandKind kind,
        IReadOnlyList<CustomApp>? apps = null,
        CustomApp? app = null,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        bool shouldOpenCursorAtFolder = false,
        string? cursorFolderPath = null) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Apps = apps ?? Array.Empty<CustomApp>(),
            App = app,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            CursorFolderPath = cursorFolderPath
        };

    public static AppCommandResult Fail(AppCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
