using System.Net.Http;
using SecretBase.Core.Calendar;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Google Calendar via user-provided secret iCal (ICS) HTTPS URL.
/// No OAuth client secrets in the app — the user pastes the private ICS address
/// from Google Calendar settings. Maps ICS → common <see cref="CalendarEvent"/>.
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

    public string DisplayName => "Google Calendar";

    public string? OpenUrl => "https://calendar.google.com/";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_icsUrl)
        && Uri.TryCreate(_icsUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

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
        return parsed.Where(e => Overlaps(e, query)).OrderBy(e => e.Start).ToList();
    }

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
