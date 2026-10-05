namespace SecretBase.Core.Calendar;

/// <summary>
/// Optional write surface for calendar providers that can create events.
/// Core stays free of OAuth / HTTP — Infrastructure implements this on Google.
/// </summary>
public interface ICalendarEventWriter
{
    Task<CalendarEvent> CreateEventAsync(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        bool isAllDay = false,
        CancellationToken cancellationToken = default);
}
