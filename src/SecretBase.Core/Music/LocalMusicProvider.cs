namespace SecretBase.Core.Music;

/// <summary>
/// Local music placeholder. No filesystem scanning, no player yet.
/// Keeps a provider slot for a future user-selected library.
/// </summary>
public sealed class LocalMusicProvider : IMusicProvider
{
    public static LocalMusicProvider Instance { get; } = new();

    public string ProviderId => "local";

    public string DisplayName => "Local";

    public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.None;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.NotApplicable;

    public bool CanHandle(MusicSourceType type) => type == MusicSourceType.Local;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = "Local music player is not available yet. A future Local Music Provider will use user-selected folders only — never unrestricted scanning.";
        return false;
    }
}
