namespace SecretBase.Core.Music;

/// <summary>
/// Future boundary for Spotify API / YouTube Data / Local library providers.
/// This phase only needs open-in-widget URL resolution — no OAuth, no Host Bridge.
/// </summary>
public interface IMusicProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    MusicProviderCapabilities Capabilities { get; }

    MusicAuthStatus AuthStatus { get; }

    bool CanHandle(MusicSourceType type);

    /// <summary>
    /// Maps a source to a safe http(s) URL for the Music Widget WebView.
    /// Returns false for Local / unsupported / invalid URLs (with error message).
    /// </summary>
    bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error);
}
