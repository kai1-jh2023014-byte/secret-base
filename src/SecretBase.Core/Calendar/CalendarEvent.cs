namespace SecretBase.Core.Calendar;

/// <summary>
/// Common calendar event model. Provider-specific payloads must be mapped here —
/// never stored as Core domain entities.
/// </summary>
public sealed class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Stable provider key, e.g. <see cref="CalendarProviderIds.Local"/>.</summary>
    public string Provider { get; set; } = CalendarProviderIds.Local;

    public string? CalendarId { get; set; }

    public string? CalendarName { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public bool IsAllDay { get; set; }

    public string? Location { get; set; }

    public string? Description { get; set; }

    public string? Url { get; set; }

    /// <summary>Optional accent (#RRGGBB) for agenda color dots.</summary>
    public string? Color { get; set; }

    /// <summary>Human-readable source label (e.g. "Google Calendar · School").</summary>
    public string? Source { get; set; }

    public DateTimeOffset? LastUpdated { get; set; }

    public DateOnly LocalDate => ToCalendarDate(Start);

    public bool OccursOn(DateOnly localDate)
    {
        // Calendar date in the event's own offset — not machine local TZ.
        var startDate = ToCalendarDate(Start);
        var endInstant = End;
        var endDate = ToCalendarDate(endInstant);
        if (IsAllDay && endInstant > Start && IsMidnightInOffset(endInstant))
        {
            endDate = endDate.AddDays(-1);
        }

        if (endDate < startDate)
        {
            endDate = startDate;
        }

        return localDate >= startDate && localDate <= endDate;
    }

    private static DateOnly ToCalendarDate(DateTimeOffset instant) =>
        new(instant.Year, instant.Month, instant.Day);

    private static bool IsMidnightInOffset(DateTimeOffset instant) =>
        instant.Hour == 0 && instant.Minute == 0 && instant.Second == 0 && instant.Millisecond == 0;
}
