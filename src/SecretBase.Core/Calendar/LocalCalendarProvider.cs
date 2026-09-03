namespace SecretBase.Core.Calendar;

/// <summary>Store-backed local provider. No network. Create/delete affect Secret Base storage only.</summary>
public sealed class LocalCalendarProvider : ICalendarProvider
{
    private readonly ILocalCalendarStore _store;

    public LocalCalendarProvider(
        IEnumerable<CalendarEvent>? events = null,
        string displayName = "Local",
        string? openUrl = null)
        : this(new MemoryLocalCalendarStore(events), displayName, openUrl)
    {
    }

    public LocalCalendarProvider(
        ILocalCalendarStore store,
        string displayName = "Local",
        string? openUrl = null,
        IEnumerable<CalendarEvent>? seedIfEmpty = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        DisplayName = displayName;
        OpenUrl = openUrl;
        if (seedIfEmpty is not null && _store.LoadEvents().Count == 0)
        {
            _store.SaveEvents(seedIfEmpty.Select(Clone).ToList());
        }
    }

    public string ProviderId => CalendarProviderIds.Local;

    public string DisplayName { get; }

    public string? OpenUrl { get; }

    public CalendarProviderCapabilities Capabilities =>
        CalendarProviderCapabilities.ReadEvents
        | CalendarProviderCapabilities.ListCalendars
        | CalendarProviderCapabilities.CreateEvents
        | CalendarProviderCapabilities.DeleteEvents;

    public CalendarAuthStatus AuthStatus => CalendarAuthStatus.NotApplicable;

    public bool IsConfigured => true;

    public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CalendarInfo> list =
        [
            new CalendarInfo
            {
                Id = "local",
                Name = DisplayName,
                ProviderId = ProviderId,
                Color = "#4A90D9",
                IsPrimary = true
            }
        ];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CalendarEvent> result = _store.LoadEvents()
            .Where(e => OverlapsQuery(e, query))
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();
        return Task.FromResult(result);
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public IReadOnlyList<CalendarEvent> ListAll() =>
        _store.LoadEvents()
            .OrderBy(e => e.Start)
            .Select(Clone)
            .ToList();

    public CalendarEvent AddEvent(string title, DateTimeOffset start, DateTimeOffset end, bool isAllDay = false)
    {
        var ev = Clone(new CalendarEvent
        {
            Title = title.Trim(),
            Start = start,
            End = end <= start ? start.AddHours(1) : end,
            IsAllDay = isAllDay,
            Provider = CalendarProviderIds.Local,
            CalendarId = "local",
            CalendarName = DisplayName,
            Source = DisplayName,
            LastUpdated = DateTimeOffset.UtcNow
        });
        var list = _store.LoadEvents().Select(Clone).ToList();
        list.Add(ev);
        _store.SaveEvents(list);
        return ev;
    }

    public bool TryRemoveEvent(string eventId, out CalendarEvent? removed)
    {
        removed = null;
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        var list = _store.LoadEvents().Select(Clone).ToList();
        var match = list.FirstOrDefault(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        list.RemoveAll(e => string.Equals(e.Id, eventId, StringComparison.OrdinalIgnoreCase));
        _store.SaveEvents(list);
        removed = match;
        return true;
    }

    public IReadOnlyList<UsualScheduleSlot> ListUsual() => _store.LoadUsual();

    public UsualScheduleSlot RememberUsual(string title, int hour, int minute, int durationMinutes, bool isAllDay)
    {
        var slot = new UsualScheduleSlot
        {
            Title = title.Trim(),
            Hour = Math.Clamp(hour, 0, 23),
            Minute = Math.Clamp(minute, 0, 59),
            DurationMinutes = durationMinutes <= 0 ? 60 : Math.Clamp(durationMinutes, 15, 480),
            IsAllDay = isAllDay
        };
        var list = _store.LoadUsual().ToList();
        var existing = list.FindIndex(s => s.Title.Equals(slot.Title, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            slot.Id = list[existing].Id;
            list[existing] = slot;
        }
        else
        {
            list.Add(slot);
        }

        _store.SaveUsual(list);
        return slot;
    }

    public IReadOnlyList<CalendarEvent> ApplyUsual(DateOnly day, TimeSpan offset)
    {
        var slots = _store.LoadUsual();
        if (slots.Count == 0)
        {
            return [];
        }

        var created = new List<CalendarEvent>();
        foreach (var slot in slots)
        {
            DateTimeOffset start;
            DateTimeOffset end;
            if (slot.IsAllDay)
            {
                start = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), offset);
                end = start.AddDays(1);
            }
            else
            {
                start = new DateTimeOffset(day.ToDateTime(new TimeOnly(slot.Hour, slot.Minute)), offset);
                end = start.AddMinutes(slot.DurationMinutes <= 0 ? 60 : slot.DurationMinutes);
            }

            created.Add(AddEvent(slot.Title, start, end, slot.IsAllDay));
        }

        return created;
    }

    public void ReplaceAll(IEnumerable<CalendarEvent> events)
    {
        _store.SaveEvents(events.Select(Clone).ToList());
    }

    private static bool OverlapsQuery(CalendarEvent ev, CalendarQuery query)
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

    private static CalendarEvent Clone(CalendarEvent source) =>
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
}
