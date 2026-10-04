using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Music;

/// <summary>
/// Builds an https Spotify search page. Free accounts can open this in the browser.
/// The Web API catalog is a separate path and is not used here.
/// </summary>
public static class SpotifyWebSearch
{
    public static bool TryCreateSearchUrl(string? query, out string? url, out string? error)
    {
        url = null;
        error = null;
        var text = query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MusicCommandService.MaxQueryLength)
        {
            error = "Search query is empty.";
            return false;
        }

        if (text.Contains("://", StringComparison.Ordinal)
            || text.Contains("..", StringComparison.Ordinal)
            || text.StartsWith('\\')
            || text.StartsWith('/'))
        {
            error = "Search query is not allowed.";
            return false;
        }

        var candidate = "https://open.spotify.com/search/" + Uri.EscapeDataString(text);
        if (!WebUrlValidator.TryNormalize(candidate, out var normalized, out var validationError)
            || string.IsNullOrWhiteSpace(normalized)
            || !normalized.StartsWith("https://open.spotify.com/search/", StringComparison.OrdinalIgnoreCase))
        {
            error = validationError ?? WebUrlValidator.BlockedMessage;
            return false;
        }

        url = normalized;
        return true;
    }
}
