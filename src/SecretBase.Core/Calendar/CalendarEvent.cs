namespace SecretBase.Core.Calendar;

/// <summary>
/// A single calendar entry from any <see cref="ICalendarEventSource"/>.
/// Local-only in v0.1 — no cloud credentials in Core.
/// </summary>
public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Local calendar date of the event (all-day style for v0.1).</summary>
    public DateOnly Date { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
