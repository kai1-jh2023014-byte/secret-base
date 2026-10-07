using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Music;

/// <summary>
/// Builds https Spotify pages (search / track / album / artist). Free accounts can open these
/// in the browser or the Spotify desktop app via https. Playback Web API is a separate path.
/// </summary>
public static class SpotifyWebSearch
{
    private const string OpenBase = "https://open.spotify.com/";

    public static bool TryCreateSearchUrl(string? query, out string? url, out string? error) =>
        TryCreatePathUrl("search", query, requireSpotifyId: false, out url, out error);

    public static bool TryCreateTrackUrl(string? trackId, out string? url, out string? error) =>
        TryCreatePathUrl("track", trackId, requireSpotifyId: true, out url, out error);

    public static bool TryCreateAlbumUrl(string? albumId, out string? url, out string? error) =>
        TryCreatePathUrl("album", albumId, requireSpotifyId: true, out url, out error);

    public static bool TryCreateArtistUrl(string? artistId, out string? url, out string? error) =>
        TryCreatePathUrl("artist", artistId, requireSpotifyId: true, out url, out error);

    /// <summary>
    /// Prefers a track page when a Spotify id is known; otherwise a search page
    /// from the track metadata or free-text query.
    /// </summary>
    public static bool TryCreateOpenUrl(
        MusicTrack? track,
        string? query,
        out string? url,
        out string? error)
    {
        url = null;
        error = null;

        if (track is not null
            && LooksLikeSpotifyProvider(track.ProviderId)
            && !string.IsNullOrWhiteSpace(track.Id)
            && TryCreateTrackUrl(track.Id, out url, out error))
        {
            return true;
        }

        var searchText = BuildSearchText(track, query);
        return TryCreateSearchUrl(searchText, out url, out error);
    }

    /// <summary>spotify:track:… style URI for hosts that can launch the desktop app directly.</summary>
    public static bool TryCreateDesktopUri(MusicTrack? track, string? query, out string? uri, out string? error)
    {
        uri = null;
        error = null;
        if (track is not null
            && LooksLikeSpotifyProvider(track.ProviderId)
            && IsSpotifyResourceId(track.Id))
        {
            uri = "spotify:track:" + track.Id.Trim();
            return true;
        }

        var searchText = BuildSearchText(track, query);
        if (string.IsNullOrWhiteSpace(searchText) || searchText.Length > MusicCommandService.MaxQueryLength)
        {
            error = "Search query is empty.";
            return false;
        }

        if (!IsSafeQuery(searchText))
        {
            error = "Search query is not allowed.";
            return false;
        }

        uri = "spotify:search:" + Uri.EscapeDataString(searchText);
        return true;
    }

    private static bool TryCreatePathUrl(
        string kind,
        string? value,
        bool requireSpotifyId,
        out string? url,
        out string? error)
    {
        url = null;
        error = null;
        var text = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MusicCommandService.MaxQueryLength)
        {
            error = kind == "search" ? "Search query is empty." : "Spotify id is empty.";
            return false;
        }

        if (requireSpotifyId)
        {
            if (!IsSpotifyResourceId(text))
            {
                error = "Spotify id is not valid.";
                return false;
            }
        }
        else if (!IsSafeQuery(text))
        {
            error = "Search query is not allowed.";
            return false;
        }

        var segment = kind == "search" ? Uri.EscapeDataString(text) : text;
        var candidate = OpenBase + kind + "/" + segment;
        if (!WebUrlValidator.TryNormalize(candidate, out var normalized, out var validationError)
            || string.IsNullOrWhiteSpace(normalized)
            || !normalized.StartsWith(OpenBase + kind + "/", StringComparison.OrdinalIgnoreCase))
        {
            error = validationError ?? WebUrlValidator.BlockedMessage;
            return false;
        }

        url = normalized;
        return true;
    }

    private static string BuildSearchText(MusicTrack? track, string? query)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            return query.Trim();
        }

        if (track is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(track.Title) && !string.IsNullOrWhiteSpace(track.Artist))
        {
            return $"{track.Artist} {track.Title}".Trim();
        }

        if (!string.IsNullOrWhiteSpace(track.Title))
        {
            return track.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(track.Artist))
        {
            return track.Artist.Trim();
        }

        if (!string.IsNullOrWhiteSpace(track.Album))
        {
            return track.Album.Trim();
        }

        return string.Empty;
    }

    private static bool IsSafeQuery(string text) =>
        !text.Contains("://", StringComparison.Ordinal)
        && !text.Contains("..", StringComparison.Ordinal)
        && !text.StartsWith('\\')
        && !text.StartsWith('/');

    internal static bool LooksLikeSpotifyProvider(string? providerId) =>
        string.Equals(providerId, "spotify", StringComparison.OrdinalIgnoreCase);

    /// <summary>Spotify resource ids are typically 22 base62 characters.</summary>
    internal static bool IsSpotifyResourceId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var text = id.Trim();
        if (text.Length is < 10 or > 64)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
