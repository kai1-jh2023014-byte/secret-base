namespace SecretBase.Core.Calendar;

/// <summary>In-memory local calendar store for tests and hosts that do not persist yet.</summary>
public sealed class MemoryLocalCalendarStore : ILocalCalendarStore
{
    private readonly object _gate = new();
    private List<CalendarEvent> _events;
    private List<UsualScheduleSlot> _usual;

    public MemoryLocalCalendarStore(
        IEnumerable<CalendarEvent>? events = null,
        IEnumerable<UsualScheduleSlot>? usual = null)
    {
        _events = events?.Select(CloneEvent).ToList() ?? [];
        _usual = usual?.Select(CloneUsual).ToList() ?? [];
    }

    public IReadOnlyList<CalendarEvent> LoadEvents()
    {
        lock (_gate)
        {
            return _events.Select(CloneEvent).ToList();
        }
    }

    public void SaveEvents(IReadOnlyList<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        lock (_gate)
        {
            _events = events.Select(CloneEvent).ToList();
        }
    }

    public IReadOnlyList<UsualScheduleSlot> LoadUsual()
    {
        lock (_gate)
        {
            return _usual.Select(CloneUsual).ToList();
        }
    }

    public void SaveUsual(IReadOnlyList<UsualScheduleSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        lock (_gate)
        {
            _usual = slots.Select(CloneUsual).ToList();
        }
    }

    private static CalendarEvent CloneEvent(CalendarEvent source) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
            Provider = string.IsNullOrWhiteSpace(source.Provider) ? CalendarProviderIds.Local : source.Provider,
            CalendarId = source.CalendarId,
            CalendarName = source.CalendarName,
            Title = source.Title ?? string.Empty,
            Start = source.Start,
            End = source.End,
            IsAllDay = source.IsAllDay,
            Location = source.Location,
            Description = source.Description,
            Url = source.Url,
            Color = source.Color,
            Source = source.Source ?? source.CalendarName ?? "Local",
            LastUpdated = source.LastUpdated
        };

    private static UsualScheduleSlot CloneUsual(UsualScheduleSlot source) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(source.Id) ? Guid.NewGuid().ToString("N") : source.Id,
            Title = source.Title ?? string.Empty,
            Hour = source.Hour,
            Minute = source.Minute,
            DurationMinutes = source.DurationMinutes <= 0 ? 60 : source.DurationMinutes,
            IsAllDay = source.IsAllDay
        };
}
