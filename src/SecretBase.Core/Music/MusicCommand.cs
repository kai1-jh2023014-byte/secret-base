namespace SecretBase.Core.Music;

/// <summary>Allowed Music operations for UI and future AI (never OS / FS / process).</summary>
public enum MusicCommandKind
{
    SearchTrack = 0,
    PlayTrack = 1,
    Pause = 2,
    Resume = 3,
    Next = 4,
    Previous = 5,
    GetPlaybackState = 6,
    ConnectProvider = 7,
    DisconnectProvider = 8
}

/// <summary>
/// Validated music intent. AI (future) must emit these — never call providers or OS directly.
/// </summary>
public sealed class MusicCommand
{
    public MusicCommandKind Kind { get; init; }

    /// <summary>Search query (SearchTrack).</summary>
    public string? Query { get; init; }

    /// <summary>Track id to play (PlayTrack).</summary>
    public string? TrackId { get; init; }

    /// <summary>Optional provider filter / owner for PlayTrack.</summary>
    public string? ProviderId { get; init; }

    /// <summary>Optional resolved track for PlayTrack (preferred over TrackId lookup).</summary>
    public MusicTrack? Track { get; init; }

    /// <summary>OAuth client id for ConnectProvider. Never logged.</summary>
    public string? ClientId { get; init; }

    /// <summary>Optional OAuth client secret for ConnectProvider. Never logged.</summary>
    public string? ClientSecret { get; init; }

    public static MusicCommand SearchTrack(string query, string? providerId = null) =>
        new() { Kind = MusicCommandKind.SearchTrack, Query = query, ProviderId = providerId };

    public static MusicCommand PlayTrack(MusicTrack track) =>
        new() { Kind = MusicCommandKind.PlayTrack, Track = track, TrackId = track.Id, ProviderId = track.ProviderId };

    public static MusicCommand PlayTrackById(string trackId, string providerId) =>
        new() { Kind = MusicCommandKind.PlayTrack, TrackId = trackId, ProviderId = providerId };

    public static MusicCommand Pause() => new() { Kind = MusicCommandKind.Pause };

    public static MusicCommand Resume() => new() { Kind = MusicCommandKind.Resume };

    public static MusicCommand Next() => new() { Kind = MusicCommandKind.Next };

    public static MusicCommand Previous() => new() { Kind = MusicCommandKind.Previous };

    public static MusicCommand GetPlaybackState(string? providerId = null) =>
        new() { Kind = MusicCommandKind.GetPlaybackState, ProviderId = providerId };

    public static MusicCommand ConnectProvider(string providerId, string? clientId = null, string? clientSecret = null) =>
        new()
        {
            Kind = MusicCommandKind.ConnectProvider,
            ProviderId = providerId,
            ClientId = clientId,
            ClientSecret = clientSecret
        };

    public static MusicCommand DisconnectProvider(string providerId) =>
        new() { Kind = MusicCommandKind.DisconnectProvider, ProviderId = providerId };
}
