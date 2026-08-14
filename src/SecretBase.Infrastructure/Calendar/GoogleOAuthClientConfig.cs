using System.Text.Json;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>Desktop OAuth client config — loaded from AppData, never committed.</summary>
public sealed class GoogleOAuthClientConfig
{
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Desktop clients may include a client_secret; treat as sensitive.</summary>
    public string? ClientSecret { get; set; }

    public static string DefaultClientConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SecretBase",
            "credentials",
            "google-oauth-client.json");

    public static bool TryLoad(string? path, out GoogleOAuthClientConfig? config, out string? error)
    {
        config = null;
        error = null;
        var resolved = string.IsNullOrWhiteSpace(path) ? DefaultClientConfigPath : path.Trim();
        if (!File.Exists(resolved))
        {
            error = "OAuth client file not found.";
            return false;
        }

        path = resolved;

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            // Support both flat {client_id} and Google download shape {installed:{client_id}}
            if (root.TryGetProperty("installed", out var installed))
            {
                root = installed;
            }
            else if (root.TryGetProperty("web", out var web))
            {
                root = web;
            }

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

            config = new GoogleOAuthClientConfig
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
