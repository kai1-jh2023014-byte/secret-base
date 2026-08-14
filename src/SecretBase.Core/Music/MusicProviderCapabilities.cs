namespace SecretBase.Core.Music;

/// <summary>What a music provider can do. UI disables unsupported ops; never fake APIs.</summary>
[Flags]
public enum MusicProviderCapabilities
{
    None = 0,

    /// <summary>Can resolve an http(s) URL (optional auxiliary open — not the primary Music UI).</summary>
    OpenInWidget = 1 << 0,

    /// <summary>Reserved — OAuth / API auth.</summary>
    Authentication = 1 << 1,

    /// <summary>Catalog / track search.</summary>
    Search = 1 << 2,

    /// <summary>Expose current track metadata.</summary>
    NowPlaying = 1 << 3,

    /// <summary>Reserved — user-selected local library (never unrestricted FS scan).</summary>
    LocalLibrary = 1 << 4,

    /// <summary>Start playback of a resolved track.</summary>
    Playback = 1 << 5,

    Pause = 1 << 6,
    Resume = 1 << 7,
    Next = 1 << 8,
    Previous = 1 << 9
}
