namespace SecretBase.Core.Calendar;

/// <summary>
/// In-memory / configuration-backed event source (local-only).
/// Does not touch the filesystem or network — App/Infrastructure may persist
/// the underlying list via widget configuration JSON.
/// </summary>
public sealed class LocalCalendarEventSource : ICalendarEventSource
{
    private readonly List<CalendarEvent> _events;

    public LocalCalendarEventSource(IEnumerable<CalendarEvent>? events = null)
    {
        _events = events?.Select(Clone).ToList() ?? [];
    }

    public string SourceId => "local";

    public IReadOnlyList<CalendarEvent> Events => _events;

    public IReadOnlyList<CalendarEvent> GetEvents(DateOnly fromInclusive, DateOnly toInclusive)
    {
        if (toInclusive < fromInclusive)
        {
            return Array.Empty<CalendarEvent>();
        }

        return _events
            .Where(e => e.Date >= fromInclusive && e.Date <= toInclusive)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();
    }

    public void ReplaceAll(IEnumerable<CalendarEvent> events)
    {
        _events.Clear();
        _events.AddRange(events.Select(Clone));
    }

    private static CalendarEvent Clone(CalendarEvent source) =>
        new()
        {
            Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
            Date = source.Date,
            Title = source.Title ?? string.Empty,
            Notes = source.Notes
        };
}
