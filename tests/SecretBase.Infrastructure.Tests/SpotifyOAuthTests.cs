using SecretBase.Core.Music;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Music;

namespace SecretBase.Infrastructure.Tests;

public class SpotifyOAuthTests
{
    [Fact]
    public void ClientConfig_RoundTripsWithoutWritingSecretWhenAbsent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-spotify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "spotify-oauth-client.json");
        try
        {
            SpotifyOAuthClientConfig.Save(path, new SpotifyOAuthClientConfig { ClientId = "0123456789abcdef0123456789abcdef" });
            Assert.True(SpotifyOAuthClientConfig.TryLoad(path, out var loaded, out var error));
            Assert.Null(error);
            Assert.Equal("0123456789abcdef0123456789abcdef", loaded!.ClientId);
            Assert.Null(loaded.ClientSecret);
            Assert.DoesNotContain("client_secret", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PublicPkce_DoesNotSendBasicAuth()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token"
        };
        SpotifyMusicProvider.ApplyClientAuthentication(form, request, "0123456789abcdef0123456789abcdef", null);
        Assert.Equal("0123456789abcdef0123456789abcdef", form["client_id"]);
        Assert.False(form.ContainsKey("client_secret"));
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public void ConfidentialClient_SendsBasicAuth()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var form = new Dictionary<string, string>();
        SpotifyMusicProvider.ApplyClientAuthentication(form, request, "client", "secret");
        Assert.Equal("secret", form["client_secret"]);
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(request.Headers.Authorization?.Parameter));
    }

    [Fact]
    public void SearchUrl_UsesTheCurrentPageSizeCap()
    {
        var url = SpotifyMusicProvider.BuildSearchUrl("lilac");
        Assert.Contains("limit=10", url, StringComparison.Ordinal);
        Assert.DoesNotContain("limit=20", url, StringComparison.Ordinal);
        Assert.Equal(10, SpotifyMusicProvider.SearchPageSize);
    }

    [Fact]
    public void ForbiddenSearch_ExplainsTheDashboardAllowlist()
    {
        var message = SpotifyMusicProvider.MapSpotifyError(
            System.Net.HttpStatusCode.Forbidden,
            """{"error":{"status":403,"message":"Check settings on developer.spotify.com/dashboard, the user may not be registered."}}""");
        Assert.Contains("User Management", message, StringComparison.Ordinal);
        Assert.Contains("not be registered", message, StringComparison.Ordinal);
        Assert.DoesNotContain("does not indicate success", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RememberClient_WritesConfigBesideTheProvider()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-spotify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "spotify-oauth-client.json");
        try
        {
            var provider = new SpotifyMusicProvider(
                client: null,
                secrets: new MemorySecureSecretStore(),
                openBrowser: _ => false,
                clientConfigPath: path);
            provider.RememberClient("0123456789abcdef0123456789abcdef", null);
            Assert.Equal(MusicAuthStatus.Disconnected, provider.AuthStatus);
            Assert.True(SpotifyOAuthClientConfig.TryLoad(path, out var loaded, out _));
            Assert.Equal("0123456789abcdef0123456789abcdef", loaded!.ClientId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
