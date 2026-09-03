using System.Text.Json;

namespace SecretBase.Infrastructure.Music;

/// <summary>Spotify OAuth client config loaded from AppData — never committed.</summary>
public sealed class SpotifyOAuthClientConfig
{
    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    public static string DefaultClientConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecretBase",
            "credentials",
            "spotify-oauth-client.json");

    public static bool TryLoad(string? path, out SpotifyOAuthClientConfig? config, out string? error)
    {
        config = null;
        error = null;
        var resolved = string.IsNullOrWhiteSpace(path) ? DefaultClientConfigPath : path.Trim();
        if (!File.Exists(resolved))
        {
            error = "Spotify OAuth client file not found.";
            return false;
        }

        try
        {
            using var stream = File.OpenRead(resolved);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var id = root.TryGetProperty("client_id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "client_id missing.";
                return false;
            }

            string? secret = null;
            if (root.TryGetProperty("client_secret", out var secretEl) && secretEl.ValueKind == JsonValueKind.String)
            {
                secret = secretEl.GetString();
            }

            config = new SpotifyOAuthClientConfig
            {
                ClientId = id!,
                ClientSecret = string.IsNullOrWhiteSpace(secret) ? null : secret
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
