namespace SecretBase.Core.Calendar;

/// <summary>Empty provider — used when no local events are configured.</summary>
public sealed class EmptyCalendarEventSource : ICalendarEventSource
{
    public static EmptyCalendarEventSource Instance { get; } = new();

    public string SourceId => "empty";

    public IReadOnlyList<CalendarEvent> GetEvents(DateOnly fromInclusive, DateOnly toInclusive) =>
        Array.Empty<CalendarEvent>();
}
