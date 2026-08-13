namespace SecretBase.Core.Widgets.Web;

/// <summary>
/// OS-independent URL gate for Web Widget. Allows only http(s) absolute URLs.
/// Rejects file / javascript / data / and other dangerous schemes.
/// </summary>
public static class WebUrlValidator
{
    public const string BlockedMessage = "This URL cannot be opened in Secret Base.";

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "https"
    };

    public static bool IsAllowedScheme(string? scheme) =>
        !string.IsNullOrWhiteSpace(scheme) && AllowedSchemes.Contains(scheme);

    /// <summary>
    /// Normalizes user input into an absolute http(s) URL, or returns a blocked error.
    /// Bare hosts like <c>www.youtube.com</c> are treated as https.
    /// </summary>
    public static bool TryNormalize(string? input, out string? normalizedUrl, out string? errorMessage)
    {
        normalizedUrl = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = BlockedMessage;
            return false;
        }

        var trimmed = input.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            // Allow "youtube.com/..." without an explicit scheme.
            if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out uri))
            {
                errorMessage = BlockedMessage;
                return false;
            }
        }

        if (!IsAllowedScheme(uri.Scheme))
        {
            errorMessage = BlockedMessage;
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            errorMessage = BlockedMessage;
            return false;
        }

        // Rebuild to drop oddities (e.g. userinfo abuse) while keeping path/query/fragment.
        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty
        };

        normalizedUrl = builder.Uri.AbsoluteUri;
        return true;
    }

    public static bool IsAllowed(string? input) =>
        TryNormalize(input, out _, out _);
}
