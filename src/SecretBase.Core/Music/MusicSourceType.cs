namespace SecretBase.Core.Music;

/// <summary>Built-in music source kinds. Plugin/provider IDs stay separate later.</summary>
public enum MusicSourceType
{
    /// <summary>Spotify web (open.spotify.com). No API/OAuth in this phase.</summary>
    Spotify = 0,

    /// <summary>YouTube / YouTube Music web. Music-scoped, not a generic Web Widget.</summary>
    YouTube = 1,

    /// <summary>Arbitrary http(s) music-related page.</summary>
    Web = 2,

    /// <summary>Placeholder for a future local library/player — no filesystem scan.</summary>
    Local = 3
}
