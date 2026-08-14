using System.Net.Http;
using SecretBase.Core.Calendar;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Calendar;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>
/// Builds a <see cref="CalendarService"/> from widget configuration.
/// Local events always included; Google ICS added when URL is configured.
/// </summary>
public static class CalendarServiceFactory
{
    public static CalendarService Create(
        CalendarWidgetConfiguration configuration,
        ITimeProvider timeProvider,
        HttpClient? httpClient = null)
    {
        var providers = new List<ICalendarProvider>();

        var localEvents = configuration.Events.ToList();
        if (localEvents.Count == 0 && configuration.UseSampleAgendaWhenEmpty
            && string.IsNullOrWhiteSpace(configuration.GoogleIcsUrl))
        {
            var now = timeProvider.GetLocalNow();
            var day = DateOnly.FromDateTime(now.DateTime);
            localEvents.AddRange(SampleCalendarEvents.CreateCreativeDay(day, now.Offset));
        }

        providers.Add(new LocalCalendarProvider(
            localEvents,
            displayName: "Local",
            openUrl: configuration.OpenCalendarUrl));

        if (!string.IsNullOrWhiteSpace(configuration.GoogleIcsUrl))
        {
            providers.Add(new GoogleCalendarIcsProvider(configuration.GoogleIcsUrl, httpClient));
        }

        return new CalendarService(providers);
    }
}
