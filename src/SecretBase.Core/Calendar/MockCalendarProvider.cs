namespace SecretBase.Core.Calendar;

/// <summary>Deterministic colored agenda for UI/tests (no network).</summary>
public sealed class MockCalendarProvider : ICalendarProvider
{
    private readonly Func<DateTimeOffset> _now;

    public MockCalendarProvider(Func<DateTimeOffset>? now = null)
    {
        _now = now ?? (() => DateTimeOffset.Now);
    }

    public string ProviderId => "mock";

    public string DisplayName => "Mock Calendar";

    public string? OpenUrl => null;

    public CalendarProviderCapabilities Capabilities =>
        CalendarProviderCapabilities.ReadEvents
        | CalendarProviderCapabilities.ListCalendars
        | CalendarProviderCapabilities.MultipleCalendars;

    public CalendarAuthStatus AuthStatus => CalendarAuthStatus.NotApplicable;

    public bool IsConfigured => true;

    public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CalendarInfo> list =
        [
            new CalendarInfo { Id = "school", Name = "School", ProviderId = ProviderId, Color = "#E67E22", IsPrimary = true },
            new CalendarInfo { Id = "creative", Name = "Creative", ProviderId = ProviderId, Color = "#8E44AD", IsPrimary = false }
        ];
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        var day = query.FromInclusive;
        var offset = _now().Offset;
        IReadOnlyList<CalendarEvent> all =
        [
            Ev(day, 9, 0, 12, 0, "Study", "school", "School", "#E67E22"),
            Ev(day, 13, 0, 15, 0, "School", "school", "School", "#E67E22"),
            Ev(day, 16, 0, 17, 0, "Guitar", "creative", "Creative", "#8E44AD"),
            Ev(day, 19, 0, 21, 0, "Programming", "creative", "Creative", "#8E44AD")
        ];

        IReadOnlyList<CalendarEvent> filtered = all.Where(e => e.OccursOn(day)).ToList();
        return Task.FromResult(filtered);

        CalendarEvent Ev(DateOnly d, int sh, int sm, int eh, int em, string title, string calId, string calName, string color) =>
            new()
            {
                Id = $"mock-{title}-{d:yyyyMMdd}",
                Provider = ProviderId,
                CalendarId = calId,
                CalendarName = calName,
                Title = title,
                Start = new DateTimeOffset(d.ToDateTime(new TimeOnly(sh, sm)), offset),
                End = new DateTimeOffset(d.ToDateTime(new TimeOnly(eh, em)), offset),
                Color = color,
                Source = $"Mock · {calName}",
                LastUpdated = _now()
            };
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
