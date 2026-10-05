using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecretBase.Core.Calendar;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Google Calendar API v3 provider (OAuth 2.0 loopback + PKCE).
/// Read + create events. Requires user-supplied OAuth client JSON under AppData.
/// Tokens via <see cref="ISecureSecretStore"/>. Never logs token values.
/// </summary>
public sealed class GoogleCalendarApiProvider : ICalendarProvider, ICalendarEventWriter
{
    /// <summary>Legacy read-only scope (older tokens). New auth requests events scope.</summary>
    public const string ReadonlyScope = "https://www.googleapis.com/auth/calendar.readonly";

    public const string EventsScope = "https://www.googleapis.com/auth/calendar.events";

    public const string SecretKeyRefresh = CalendarSecretKeys.GoogleRefreshToken;

    private readonly GoogleOAuthClientConfig? _client;
    private readonly ISecureSecretStore _secrets;
    private readonly HttpClient _http;
    private readonly Func<string, bool> _openBrowser;
    private string? _accessToken;
    private DateTimeOffset _accessExpires = DateTimeOffset.MinValue;
    private CalendarAuthStatus _authStatus;

    public GoogleCalendarApiProvider(
        GoogleOAuthClientConfig? client,
        ISecureSecretStore secrets,
        Func<string, bool> openBrowser,
        HttpClient? httpClient = null)
    {
        _client = client;
        _secrets = secrets;
        _openBrowser = openBrowser;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _authStatus = ResolveInitialStatus();
    }

    public string ProviderId => CalendarProviderIds.Google + "-api";

    public string DisplayName => "Google Calendar";

    public string? OpenUrl => "https://calendar.google.com/";

    public CalendarProviderCapabilities Capabilities =>
        CalendarProviderCapabilities.ReadEvents
        | CalendarProviderCapabilities.ListCalendars
        | CalendarProviderCapabilities.MultipleCalendars
        | CalendarProviderCapabilities.Authentication
        | CalendarProviderCapabilities.CreateEvents;

    public CalendarAuthStatus AuthStatus => _authStatus;

    public bool IsConfigured => _client is not null && !string.IsNullOrWhiteSpace(_client.ClientId);

    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _client is null)
        {
            _authStatus = CalendarAuthStatus.NotConfigured;
            throw new InvalidOperationException(
                "Google OAuth client file missing. Place google-oauth-client.json under SecretBase credentials.");
        }

        var redirect = $"http://127.0.0.1:{GetFreePort()}/";
        var verifier = CreateCodeVerifier();
        var challenge = CreateCodeChallenge(verifier);
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var authUrl =
            "https://accounts.google.com/o/oauth2/v2/auth"
            + "?response_type=code"
            + "&client_id=" + Uri.EscapeDataString(_client.ClientId)
            + "&redirect_uri=" + Uri.EscapeDataString(redirect)
            + "&scope=" + Uri.EscapeDataString(EventsScope)
            + "&code_challenge=" + Uri.EscapeDataString(challenge)
            + "&code_challenge_method=S256"
            + "&state=" + Uri.EscapeDataString(state)
            + "&access_type=offline"
            + "&prompt=consent";

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect);
        listener.Start();

        if (!_openBrowser(authUrl))
        {
            listener.Stop();
            throw new InvalidOperationException("Could not open the browser for Google sign-in.");
        }

        var contextTask = listener.GetContextAsync();
        var completed = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(3), cancellationToken))
            .ConfigureAwait(false);
        if (completed != contextTask)
        {
            listener.Stop();
            throw new TimeoutException("Google sign-in timed out.");
        }

        var context = await contextTask.ConfigureAwait(false);
        var query = ParseQuery(context.Request.Url?.Query);
        WriteHtml(context.Response, "<html><body>You can close this window and return to Secret Base.</body></html>");
        listener.Stop();

        if (!query.TryGetValue("state", out var returnedState)
            || !string.Equals(returnedState, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("OAuth state mismatch.");
        }

        if (query.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error))
        {
            _authStatus = CalendarAuthStatus.Error;
            throw new InvalidOperationException("Google authorization denied.");
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("Authorization code missing.");
        }

        await ExchangeCodeAsync(code, verifier, redirect, cancellationToken).ConfigureAwait(false);
        _authStatus = CalendarAuthStatus.Connected;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _secrets.DeleteSecret(SecretKeyRefresh);
        _accessToken = null;
        _accessExpires = DateTimeOffset.MinValue;
        _authStatus = IsConfigured ? CalendarAuthStatus.Disconnected : CalendarAuthStatus.NotConfigured;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<CalendarInfo>();
        }

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/calendar/v3/users/me/calendarList");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _authStatus = CalendarAuthStatus.Error;
            throw new InvalidOperationException($"Google calendarList failed ({(int)resp.StatusCode}).");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = new List<CalendarInfo>();
        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var name = item.TryGetProperty("summary", out var sum) ? sum.GetString() : id;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                list.Add(new CalendarInfo
                {
                    Id = id!,
                    Name = name ?? id!,
                    ProviderId = ProviderId,
                    Color = item.TryGetProperty("backgroundColor", out var color) ? color.GetString() : "#4285F4",
                    IsPrimary = item.TryGetProperty("primary", out var primary) && primary.ValueKind == JsonValueKind.True
                });
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            return Array.Empty<CalendarEvent>();
        }

        var calendars = await GetCalendarsAsync(cancellationToken).ConfigureAwait(false);
        if (calendars.Count == 0)
        {
            calendars =
            [
                new CalendarInfo { Id = "primary", Name = "Primary", ProviderId = ProviderId, Color = "#4285F4", IsPrimary = true }
            ];
        }

        var timeMin = query.FromInclusive.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var timeMax = query.ToInclusive.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var merged = new List<CalendarEvent>();

        foreach (var cal in calendars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var url =
                "https://www.googleapis.com/calendar/v3/calendars/"
                + Uri.EscapeDataString(cal.Id)
                + "/events?singleEvents=true&orderBy=startTime"
                + "&timeMin=" + Uri.EscapeDataString(timeMin.ToString("o"))
                + "&timeMax=" + Uri.EscapeDataString(timeMax.ToString("o"))
                + "&maxResults=50";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                continue;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!doc.RootElement.TryGetProperty("items", out var items))
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                if (GoogleCalendarJsonMapper.TryMapEvent(item, cal, out var ev))
                {
                    merged.Add(ev);
                }
            }
        }

        return merged.OrderBy(e => e.Start).ToList();
    }

    public async Task<CalendarEvent> CreateEventAsync(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        bool isAllDay = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Event title is required.", nameof(title));
        }

        if (!await EnsureAccessTokenAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "Google Calendar is not connected. Open the Calendar widget and connect Google.");
        }

        object body = isAllDay
            ? new
            {
                summary = title.Trim(),
                start = new { date = start.ToString("yyyy-MM-dd") },
                end = new { date = end.ToString("yyyy-MM-dd") }
            }
            : new
            {
                summary = title.Trim(),
                start = new { dateTime = start.ToString("o"), timeZone = TimeZoneInfo.Local.Id },
                end = new { dateTime = end.ToString("o"), timeZone = TimeZoneInfo.Local.Id }
            };

        var json = JsonSerializer.Serialize(body);
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            "https://www.googleapis.com/calendar/v3/calendars/primary/events")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if ((int)resp.StatusCode is 401 or 403)
            {
                _authStatus = CalendarAuthStatus.Error;
                throw new InvalidOperationException(
                    "Google Calendar refused the write. Reconnect Google in the Calendar widget (events scope required).");
            }

            throw new InvalidOperationException(
                $"Google Calendar create failed ({(int)resp.StatusCode})."
                + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " " + Truncate(detail, 160)));
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var cal = new CalendarInfo
        {
            Id = "primary",
            Name = "Primary",
            ProviderId = ProviderId,
            Color = "#4285F4",
            IsPrimary = true
        };
        if (GoogleCalendarJsonMapper.TryMapEvent(doc.RootElement, cal, out var mapped))
        {
            return mapped;
        }

        return new CalendarEvent
        {
            Id = doc.RootElement.TryGetProperty("id", out var idEl)
                ? idEl.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N"),
            Provider = ProviderId,
            CalendarId = "primary",
            CalendarName = "Primary",
            Title = title.Trim(),
            Start = start,
            End = end,
            IsAllDay = isAllDay,
            Source = "Google Calendar",
            Color = "#4285F4"
        };
    }

    private static string Truncate(string value, int max)
    {
        var trimmed = value.Replace('\n', ' ').Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max] + "…";
    }

    private CalendarAuthStatus ResolveInitialStatus()
    {
        if (!IsConfigured)
        {
            return CalendarAuthStatus.NotConfigured;
        }

        return _secrets.TryGetSecret(SecretKeyRefresh, out var token) && !string.IsNullOrEmpty(token)
            ? CalendarAuthStatus.Connected
            : CalendarAuthStatus.Disconnected;
    }

    private async Task<bool> EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            _authStatus = CalendarAuthStatus.NotConfigured;
            return false;
        }

        if (!string.IsNullOrEmpty(_accessToken) && DateTimeOffset.UtcNow < _accessExpires.AddMinutes(-1))
        {
            return true;
        }

        if (!_secrets.TryGetSecret(SecretKeyRefresh, out var refresh) || string.IsNullOrEmpty(refresh))
        {
            _authStatus = CalendarAuthStatus.Disconnected;
            return false;
        }

        try
        {
            await RefreshAccessTokenAsync(refresh!, cancellationToken).ConfigureAwait(false);
            _authStatus = CalendarAuthStatus.Connected;
            return true;
        }
        catch
        {
            _authStatus = CalendarAuthStatus.Error;
            return false;
        }
    }

    private async Task ExchangeCodeAsync(string code, string verifier, string redirect, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _client!.ClientId,
            ["redirect_uri"] = redirect,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = verifier
        };
        if (!string.IsNullOrEmpty(_client.ClientSecret))
        {
            form["client_secret"] = _client.ClientSecret!;
        }

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        ApplyTokenResponse(doc.RootElement);
    }

    private async Task RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _client!.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };
        if (!string.IsNullOrEmpty(_client.ClientSecret))
        {
            form["client_secret"] = _client.ClientSecret!;
        }

        using var content = new FormUrlEncodedContent(form);
        using var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", content, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        ApplyTokenResponse(doc.RootElement, preserveRefresh: refreshToken);
    }

    private void ApplyTokenResponse(JsonElement root, string? preserveRefresh = null)
    {
        _accessToken = root.GetProperty("access_token").GetString();
        var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
        _accessExpires = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : preserveRefresh;
        if (!string.IsNullOrEmpty(refresh))
        {
            _secrets.SetSecret(SecretKeyRefresh, refresh!);
        }
    }

    private static Dictionary<string, string> ParseQuery(string? query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(query))
        {
            return result;
        }

        var q = query[0] == '?' ? query[1..] : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                result[Uri.UnescapeDataString(part)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
            result[key] = value;
        }

        return result;
    }

    private static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    private static string CreateCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void WriteHtml(HttpListenerResponse response, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }
}
