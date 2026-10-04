using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Music;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Music;

/// <summary>
/// Spotify Web API provider (OAuth PKCE + official REST). Controls playback on the user's
/// active Spotify device — Secret Base does not stream audio or bypass DRM.
/// </summary>
public sealed class SpotifyMusicProvider : IMusicProvider, IMusicOAuthClientSource
{
    public const string Id = "spotify";

    private const string Scope =
        "user-read-playback-state user-modify-playback-state user-read-currently-playing";

    private SpotifyOAuthClientConfig? _client;
    private readonly ISecureSecretStore _secrets;
    private readonly HttpClient _http;
    private readonly Func<string, bool> _openBrowser;
    private readonly string _clientConfigPath;

    private string? _accessToken;
    private DateTimeOffset _accessExpires = DateTimeOffset.MinValue;
    private MusicTrack? _current;
    private bool _isPlaying;
    private MusicAuthStatus _authStatus;

    public SpotifyMusicProvider(
        SpotifyOAuthClientConfig? client,
        ISecureSecretStore secrets,
        Func<string, bool> openBrowser,
        HttpClient? http = null,
        string? clientConfigPath = null)
    {
        _client = client;
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _openBrowser = openBrowser ?? throw new ArgumentNullException(nameof(openBrowser));
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _clientConfigPath = string.IsNullOrWhiteSpace(clientConfigPath)
            ? SpotifyOAuthClientConfig.DefaultClientConfigPath
            : clientConfigPath;
        _authStatus = _secrets.TryGetSecret(MusicSecretKeys.SpotifyRefreshToken, out var token)
                      && !string.IsNullOrWhiteSpace(token)
            ? MusicAuthStatus.Connected
            : _client is null || string.IsNullOrWhiteSpace(_client.ClientId)
                ? MusicAuthStatus.NotConfigured
                : MusicAuthStatus.Disconnected;
    }

    public string ProviderId => Id;

    public string DisplayName => "Spotify";

    public MusicProviderCapabilities Capabilities =>
        MusicProviderCapabilities.Search
        | MusicProviderCapabilities.Playback
        | MusicProviderCapabilities.Pause
        | MusicProviderCapabilities.Resume
        | MusicProviderCapabilities.Next
        | MusicProviderCapabilities.Previous
        | MusicProviderCapabilities.NowPlaying
        | MusicProviderCapabilities.Authentication;

    public MusicAuthStatus AuthStatus => _authStatus;

    public string RedirectUri => SpotifyOAuth.RedirectUri;

    public void RememberClient(string clientId, string? clientSecret)
    {
        var id = clientId?.Trim() ?? string.Empty;
        if (id.Length is < 16 or > 64 || id.Any(c => !char.IsAsciiHexDigit(c)))
        {
            throw new ArgumentException("Spotify Client ID should be the hex id from the Spotify dashboard.");
        }

        string? secret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret.Trim();
        if (secret is { Length: > 256 } || secret?.Contains('\n') == true || secret?.Contains('\r') == true)
        {
            throw new ArgumentException("Spotify Client Secret is not valid.");
        }

        var changed = _client is null
                      || !string.Equals(_client.ClientId, id, StringComparison.Ordinal);
        _client = new SpotifyOAuthClientConfig
        {
            ClientId = id,
            ClientSecret = secret
        };
        SpotifyOAuthClientConfig.Save(_clientConfigPath, _client);
        if (!changed)
        {
            if (_authStatus == MusicAuthStatus.NotConfigured)
            {
                _authStatus = MusicAuthStatus.Disconnected;
            }

            return;
        }

        _secrets.DeleteSecret(MusicSecretKeys.SpotifyRefreshToken);
        _accessToken = null;
        _accessExpires = DateTimeOffset.MinValue;
        _authStatus = MusicAuthStatus.Disconnected;
    }

    public MusicTrack? CurrentTrack => _current;

    public bool IsPlaying => _isPlaying;

    public bool CanHandle(MusicSourceType type) => type == MusicSourceType.Spotify;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = "Spotify web URLs are opened from the Music workspace after API connect.";
        return false;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_client is null || string.IsNullOrWhiteSpace(_client.ClientId))
        {
            _authStatus = MusicAuthStatus.NotConfigured;
            throw new InvalidOperationException(
                "Spotify Client ID is missing. Enter it in Connect Spotify. Register this Redirect URI in the Spotify dashboard: "
                + SpotifyOAuth.RedirectUri);
        }

        var redirect = SpotifyOAuth.RedirectUri;
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var authUrl =
            "https://accounts.spotify.com/authorize"
            + "?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(_client.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirect)
            + "&scope=" + Uri.EscapeDataString(Scope)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=S256"
            + "&state=" + Uri.EscapeDataString(state);

        using var listener = new HttpListener();
        listener.Prefixes.Add(SpotifyOAuth.LoopbackPrefix);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException)
        {
            throw new InvalidOperationException(
                "Could not listen on "
                + SpotifyOAuth.RedirectUri
                + ". Close anything using port "
                + SpotifyOAuth.LoopbackPort
                + " and try again.");
        }

        if (!_openBrowser(authUrl))
        {
            listener.Stop();
            throw new InvalidOperationException("Could not open the browser for Spotify sign-in.");
        }

        var contextTask = listener.GetContextAsync();
        var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(3), cancellationToken))
            .ConfigureAwait(false);
        if (completed != contextTask)
        {
            listener.Stop();
            throw new TimeoutException("Spotify sign-in timed out.");
        }

        var context = await contextTask.ConfigureAwait(false);
        var query = context.Request.Url?.Query ?? string.Empty;
        var code = ParseQuery(query, "code");
        var returnedState = ParseQuery(query, "state");
        var response = context.Response;
        var html = returnedState == state && !string.IsNullOrWhiteSpace(code)
            ? "<html><body><p>Spotify connected. You can close this window.</p></body></html>"
            : "<html><body><p>Spotify sign-in failed. Return to Secret Base and try again.</p></body></html>";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        response.Close();
        listener.Stop();

        if (string.IsNullOrWhiteSpace(code))
        {
            _authStatus = MusicAuthStatus.Error;
            throw new InvalidOperationException("Spotify authorization was denied or failed.");
        }

        await ExchangeCodeAsync(code, redirect, verifier, cancellationToken).ConfigureAwait(false);
        _authStatus = MusicAuthStatus.Connected;
        await RefreshPlaybackStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _secrets.DeleteSecret(MusicSecretKeys.SpotifyRefreshToken);
        _accessToken = null;
        _accessExpires = DateTimeOffset.MinValue;
        _current = null;
        _isPlaying = false;
        _authStatus = _client is null ? MusicAuthStatus.NotConfigured : MusicAuthStatus.Disconnected;
        return Task.CompletedTask;
    }

    public async Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.spotify.com/v1/me/player/currently-playing");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            _current = null;
            _isPlaying = false;
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        _isPlaying = doc.RootElement.TryGetProperty("is_playing", out var playingEl) && playingEl.GetBoolean();
        var progress = doc.RootElement.TryGetProperty("progress_ms", out var progressEl)
            ? progressEl.GetInt64()
            : (long?)null;
        if (!doc.RootElement.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        _current = MapTrack(item, progress);
    }

    public async Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Spotify is not connected.");
        }

        var url =
            "https://api.spotify.com/v1/search?q="
            + Uri.EscapeDataString(query.Trim())
            + "&type=track&limit=20";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("tracks", out var tracksRoot)
            || !tracksRoot.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MusicTrack>();
        }

        var list = new List<MusicTrack>();
        foreach (var item in items.EnumerateArray())
        {
            list.Add(MapTrack(item));
        }

        return list;
    }

    public async Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Spotify is not connected.");
        }

        var uri = $"spotify:track:{track.Id}";
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new Dictionary<string, object?> { ["uris"] = new[] { uri } }),
            Encoding.UTF8,
            "application/json");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent || response.IsSuccessStatusCode)
        {
            _current = track;
            _isPlaying = true;
            return;
        }

        var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(MapSpotifyError(response.StatusCode, error));
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default) =>
        await SendTransportAsync(HttpMethod.Put, "https://api.spotify.com/v1/me/player/pause", cancellationToken)
            .ConfigureAwait(false);

    public async Task ResumeAsync(CancellationToken cancellationToken = default) =>
        await SendTransportAsync(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play", cancellationToken)
            .ConfigureAwait(false);

    public async Task NextAsync(CancellationToken cancellationToken = default) =>
        await SendTransportAsync(HttpMethod.Post, "https://api.spotify.com/v1/me/player/next", cancellationToken)
            .ConfigureAwait(false);

    public async Task PreviousAsync(CancellationToken cancellationToken = default) =>
        await SendTransportAsync(HttpMethod.Post, "https://api.spotify.com/v1/me/player/previous", cancellationToken)
            .ConfigureAwait(false);

    private async Task SendTransportAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Spotify is not connected.");
        }

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(MapSpotifyError(response.StatusCode, error));
        }

        await RefreshPlaybackStateAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _accessExpires.AddMinutes(-1))
        {
            return true;
        }

        if (!_secrets.TryGetSecret(MusicSecretKeys.SpotifyRefreshToken, out var refresh)
            || string.IsNullOrWhiteSpace(refresh))
        {
            _authStatus = _client is null || string.IsNullOrWhiteSpace(_client.ClientId)
                ? MusicAuthStatus.NotConfigured
                : MusicAuthStatus.Disconnected;
            return false;
        }

        if (_client is null)
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh
        };
        ApplyClientAuthentication(form, request, _client.ClientId, _client.ClientSecret);
        request.Content = new FormUrlEncodedContent(form);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _authStatus = MusicAuthStatus.Error;
            return false;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expires = doc.RootElement.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
        _accessExpires = DateTimeOffset.UtcNow.AddSeconds(expires);
        if (doc.RootElement.TryGetProperty("refresh_token", out var refreshEl))
        {
            var rotated = refreshEl.GetString();
            if (!string.IsNullOrWhiteSpace(rotated))
            {
                _secrets.SetSecret(MusicSecretKeys.SpotifyRefreshToken, rotated);
            }
        }

        _authStatus = MusicAuthStatus.Connected;
        return !string.IsNullOrWhiteSpace(_accessToken);
    }

    private async Task ExchangeCodeAsync(
        string code,
        string redirectUri,
        string verifier,
        CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new InvalidOperationException("Spotify client is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = _client.ClientId,
            ["code_verifier"] = verifier
        };
        ApplyClientAuthentication(form, request, _client.ClientId, _client.ClientSecret);
        request.Content = new FormUrlEncodedContent(form);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Spotify token exchange failed.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        var refresh = doc.RootElement.GetProperty("refresh_token").GetString();
        if (string.IsNullOrWhiteSpace(refresh))
        {
            throw new InvalidOperationException("Spotify did not return a refresh token.");
        }

        _secrets.SetSecret(MusicSecretKeys.SpotifyRefreshToken, refresh);
        _accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expires = doc.RootElement.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 3600;
        _accessExpires = DateTimeOffset.UtcNow.AddSeconds(expires);
    }

    private static MusicTrack MapTrack(JsonElement item, long? progressMs = null)
    {
        var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
        var title = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? string.Empty : string.Empty;
        var artist = item.TryGetProperty("artists", out var artistsEl)
                     && artistsEl.ValueKind == JsonValueKind.Array
                     && artistsEl.GetArrayLength() > 0
            ? artistsEl[0].TryGetProperty("name", out var artistEl) ? artistEl.GetString() ?? string.Empty : string.Empty
            : string.Empty;
        var album = item.TryGetProperty("album", out var albumEl)
                    && albumEl.TryGetProperty("name", out var albumNameEl)
            ? albumNameEl.GetString()
            : null;
        string? artwork = null;
        if (item.TryGetProperty("album", out var albumArtEl)
            && albumArtEl.TryGetProperty("images", out var images)
            && images.ValueKind == JsonValueKind.Array
            && images.GetArrayLength() > 0)
        {
            var smallest = images[images.GetArrayLength() - 1];
            if (smallest.TryGetProperty("url", out var urlEl))
            {
                artwork = urlEl.GetString();
            }
        }

        long? duration = item.TryGetProperty("duration_ms", out var durEl) ? durEl.GetInt64() : null;
        return new MusicTrack
        {
            Id = id,
            Title = title,
            Artist = artist,
            Album = album,
            ArtworkUrl = artwork,
            ProviderId = Id,
            Source = "Spotify",
            ProgressMilliseconds = progressMs,
            DurationMilliseconds = duration
        };
    }

    private static string MapSpotifyError(HttpStatusCode status, string body) =>
        status == HttpStatusCode.Forbidden && body.Contains("PREMIUM_REQUIRED", StringComparison.Ordinal)
            ? "Spotify Premium and an active device are required for playback control."
            : status == HttpStatusCode.NotFound
                ? "No active Spotify device found. Open Spotify on a device, then try again."
                : "Spotify request failed.";

    public static void ApplyClientAuthentication(
        IDictionary<string, string> form,
        HttpRequestMessage request,
        string clientId,
        string? clientSecret)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(request);
        form["client_id"] = clientId;
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            return;
        }

        form["client_secret"] = clientSecret;
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string? ParseQuery(string query, string key)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 2 && string.Equals(Uri.UnescapeDataString(pieces[0]), key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pieces[1]);
            }
        }

        return null;
    }
}
