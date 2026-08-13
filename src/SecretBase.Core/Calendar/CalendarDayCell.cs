namespace SecretBase.Core.Calendar;

/// <summary>One cell in a month grid (may be a padding day from adjacent months).</summary>
public sealed class CalendarDayCell
{
    public DateOnly Date { get; init; }

    public bool IsCurrentMonth { get; init; }

    public bool IsToday { get; init; }

    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();

    public bool HasEvents => Events.Count > 0;
}
