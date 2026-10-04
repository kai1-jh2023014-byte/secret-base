namespace SecretBase.Core.Music;

/// <summary>
/// Fixed Spotify PKCE redirect. The dashboard must list this URI exactly.
/// A random loopback port cannot be registered in advance.
/// </summary>
public static class SpotifyOAuth
{
    public const int LoopbackPort = 43821;

    public const string RedirectUri = "http://127.0.0.1:43821/callback";

    /// <summary>HttpListener prefix. The path is matched by <see cref="RedirectUri"/>.</summary>
    public const string LoopbackPrefix = "http://127.0.0.1:43821/";
}

/// <summary>
/// Provider that can store an OAuth client id before <see cref="IMusicProvider.ConnectAsync"/>.
/// Widgets pass the id through <see cref="MusicCommand"/> — they do not write credential files.
/// </summary>
public interface IMusicOAuthClientSource
{
    string RedirectUri { get; }

    void RememberClient(string clientId, string? clientSecret);
}
