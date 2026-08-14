namespace SecretBase.Core.Calendar;

/// <summary>Aggregates providers for the Calendar Widget; isolates provider failures.</summary>
public sealed class CalendarService
{
    private readonly IReadOnlyList<ICalendarProvider> _providers;

    public CalendarService(IEnumerable<ICalendarProvider> providers)
    {
        _providers = providers.ToList();
    }

    public IReadOnlyList<ICalendarProvider> Providers => _providers;

    public async Task<CalendarAgendaSnapshot> GetAgendaSnapshotAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CalendarProviderResult>();
        var merged = new List<CalendarEvent>();

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var events = await provider.GetEventsAsync(query, cancellationToken).ConfigureAwait(false);
                results.Add(new CalendarProviderResult
                {
                    ProviderId = provider.ProviderId,
                    DisplayName = provider.DisplayName,
                    Succeeded = true,
                    AuthStatus = provider.AuthStatus,
                    Events = events
                });
                merged.AddRange(events);
            }
            catch (Exception ex)
            {
                results.Add(new CalendarProviderResult
                {
                    ProviderId = provider.ProviderId,
                    DisplayName = provider.DisplayName,
                    Succeeded = false,
                    AuthStatus = provider.AuthStatus == CalendarAuthStatus.Connected
                        ? CalendarAuthStatus.Error
                        : provider.AuthStatus,
                    ErrorMessage = ex.Message
                });
            }
        }

        return new CalendarAgendaSnapshot
        {
            Events = merged
                .OrderBy(e => e.Start)
                .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ProviderResults = results
        };
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetAgendaAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        var snap = await GetAgendaSnapshotAsync(query, cancellationToken).ConfigureAwait(false);
        return snap.Events;
    }

    public Task<IReadOnlyList<CalendarEvent>> GetTodayAgendaAsync(
        DateTimeOffset localNow,
        CancellationToken cancellationToken = default) =>
        GetAgendaAsync(CalendarQuery.ForToday(localNow), cancellationToken);

    public Task<CalendarAgendaSnapshot> GetTodaySnapshotAsync(
        DateTimeOffset localNow,
        CancellationToken cancellationToken = default) =>
        GetAgendaSnapshotAsync(CalendarQuery.ForToday(localNow), cancellationToken);
}
