namespace SecretBase.Core.Calendar;

/// <summary>In-memory / configuration-backed provider. No network.</summary>
public sealed class LocalCalendarProvider : ICalendarProvider
{
    private readonly List<CalendarEvent> _events;

    public LocalCalendarProvider(
        IEnumerable<CalendarEvent>? events = null,
        string displayName = "Local",
        string? openUrl = null)
    {
        DisplayName = displayName;
        OpenUrl = openUrl;
        _events = events?.Select(Clone).ToList() ?? [];
    }

    public string ProviderId => CalendarProviderIds.Local;

    public string DisplayName { get; }

    public string? OpenUrl { get; }

    public bool IsConfigured => true;

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CalendarEvent> result = _events
            .Where(e => OverlapsQuery(e, query))
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();
        return Task.FromResult(result);
    }

    public void ReplaceAll(IEnumerable<CalendarEvent> events)
    {
        _events.Clear();
        _events.AddRange(events.Select(Clone));
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
            Url = source.Url
        };
}
