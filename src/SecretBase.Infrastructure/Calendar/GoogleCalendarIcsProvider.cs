using System.Net.Http;
using SecretBase.Core.Calendar;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Google Calendar via user-provided secret iCal (ICS) HTTPS URL.
/// Zero-OAuth fallback. Maps ICS → <see cref="CalendarEvent"/>.
/// </summary>
public sealed class GoogleCalendarIcsProvider : ICalendarProvider
{
    private readonly HttpClient _http;
    private readonly string? _icsUrl;

    public GoogleCalendarIcsProvider(string? icsUrl, HttpClient? httpClient = null)
    {
        _icsUrl = string.IsNullOrWhiteSpace(icsUrl) ? null : icsUrl.Trim();
        _http = httpClient ?? CreateDefaultClient();
    }

    public string ProviderId => CalendarProviderIds.Google;

    public string DisplayName => "Google Calendar (ICS)";

    public string? OpenUrl => "https://calendar.google.com/";

    public CalendarProviderCapabilities Capabilities => CalendarProviderCapabilities.ReadEvents;

    public CalendarAuthStatus AuthStatus =>
        IsConfigured ? CalendarAuthStatus.Connected : CalendarAuthStatus.NotConfigured;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_icsUrl)
        && Uri.TryCreate(_icsUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Task.FromResult<IReadOnlyList<CalendarInfo>>(Array.Empty<CalendarInfo>());
        }

        IReadOnlyList<CalendarInfo> list =
        [
            new CalendarInfo
            {
                Id = "ics",
                Name = "Google Calendar",
                ProviderId = ProviderId,
                Color = "#4285F4",
                IsPrimary = true
            }
        ];
        return Task.FromResult(list);
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<CalendarEvent>();
        }

        using var response = await _http.GetAsync(_icsUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var parsed = IcsCalendarParser.Parse(text, CalendarProviderIds.Google, calendarName: "Google Calendar");
        return parsed
            .Select(e =>
            {
                e.Color ??= "#4285F4";
                e.Source ??= "Google Calendar";
                e.LastUpdated ??= DateTimeOffset.UtcNow;
                return e;
            })
            .Where(e => Overlaps(e, query))
            .OrderBy(e => e.Start)
            .ToList();
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static bool Overlaps(CalendarEvent ev, CalendarQuery query)
    {
        for (var d = query.FromInclusive; d <= query.ToInclusive; d = d.AddDays(1))
        {
            if (ev.OccursOn(d))
            {
                return true;
            }
        }

        return false;
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SecretBase/0.1 (+calendar-ics)");
        return client;
    }
}
