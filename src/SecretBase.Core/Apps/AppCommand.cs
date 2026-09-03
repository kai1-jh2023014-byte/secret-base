namespace SecretBase.Core.Apps;

/// <summary>Allowed My Apps operations for UI and future AI.</summary>
public enum AppCommandKind
{
    ListApps = 0,
    OpenApp = 1,
    OpenAppInCursor = 2,
    RemoveApp = 3
}

/// <summary>Validated app intent. Future AI must emit these — never free-form exe/args.</summary>
public sealed class AppCommand
{
    public AppCommandKind Kind { get; init; }

    public string? AppId { get; init; }

    public static AppCommand ListApps() => new() { Kind = AppCommandKind.ListApps };

    public static AppCommand OpenApp(string appId) =>
        new() { Kind = AppCommandKind.OpenApp, AppId = appId };

    public static AppCommand OpenAppInCursor(string appId) =>
        new() { Kind = AppCommandKind.OpenAppInCursor, AppId = appId };

    /// <summary>Unregisters a My App. Never deletes the application on disk.</summary>
    public static AppCommand RemoveApp(string appId) =>
        new() { Kind = AppCommandKind.RemoveApp, AppId = appId };
}
