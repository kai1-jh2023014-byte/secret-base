namespace SecretBase.Core.Music;

/// <summary>What a music provider can do. Not every provider supports APIs.</summary>
[Flags]
public enum MusicProviderCapabilities
{
    None = 0,

    /// <summary>Can resolve an http(s) URL to open inside the Music Widget WebView.</summary>
    OpenInWidget = 1 << 0,

    /// <summary>Reserved — OAuth / API auth (not implemented this phase).</summary>
    Authentication = 1 << 1,

    /// <summary>Reserved — catalog search.</summary>
    Search = 1 << 2,

    /// <summary>Reserved — now-playing / transport.</summary>
    NowPlaying = 1 << 3,

    /// <summary>Reserved — user-selected local library (never unrestricted FS scan).</summary>
    LocalLibrary = 1 << 4
}
