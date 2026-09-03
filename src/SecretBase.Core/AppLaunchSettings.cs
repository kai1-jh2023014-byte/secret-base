namespace SecretBase.Core;

/// <summary>
/// User preferences for application launch behavior (not assistant AI settings).
/// </summary>
public sealed class AppLaunchSettings
{
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// When true, Secret Base should be registered in the current user's Windows startup list.
    /// </summary>
    public bool LaunchAtWindowsLogin { get; set; }

    /// <summary>
    /// Overlay AI command bar above the Windows taskbar (not Windows Search).
    /// </summary>
    public bool TaskbarAiChatEnabled { get; set; } = true;
}
