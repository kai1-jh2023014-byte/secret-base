namespace SecretBase.Core.Music;

/// <summary>
/// Currently playing media as reported by the OS media session.
/// No tokens, no artwork URLs, and no player-specific API.
/// </summary>
public sealed class SystemNowPlaying
{
    public string Title { get; init; } = string.Empty;

    public string Artist { get; init; } = string.Empty;

    public string? Album { get; init; }

    /// <summary>OS source id (AUMID or executable). Never logged.</summary>
    public string SourceAppId { get; init; } = string.Empty;

    public bool IsPlaying { get; init; }

    public long PositionMilliseconds { get; init; }

    public long DurationMilliseconds { get; init; }

    /// <summary>Thumbnail bytes from the session, when the player published one.</summary>
    public byte[]? Artwork { get; init; }

    public string SourceName => SystemNowPlayingSourceNames.DisplayName(SourceAppId);
}

public static class SystemNowPlayingSourceNames
{
    public static string DisplayName(string? sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
        {
            return "This PC";
        }

        var id = sourceAppId.Trim();
        if (id.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
        {
            return "Spotify";
        }

        var bang = id.LastIndexOf('!');
        var leaf = bang >= 0 && bang < id.Length - 1 ? id[(bang + 1)..] : id;
        var slash = Math.Max(leaf.LastIndexOf('\\'), leaf.LastIndexOf('/'));
        if (slash >= 0 && slash < leaf.Length - 1)
        {
            leaf = leaf[(slash + 1)..];
        }

        if (leaf.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            leaf = leaf[..^4];
        }

        return string.IsNullOrWhiteSpace(leaf) ? "This PC" : leaf;
    }
}

/// <summary>
/// Reads the OS now-playing session (Windows SMTC on the Windows host).
/// Widgets never call player APIs themselves.
/// </summary>
public interface ISystemNowPlayingSource
{
    Task<SystemNowPlaying?> ReadCurrentAsync(CancellationToken cancellationToken = default);

    Task<bool> TryTogglePlayPauseAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipNextAsync(CancellationToken cancellationToken = default);

    Task<bool> TrySkipPreviousAsync(CancellationToken cancellationToken = default);
}
