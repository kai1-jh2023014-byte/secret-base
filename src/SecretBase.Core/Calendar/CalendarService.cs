namespace SecretBase.Core.Calendar;

/// <summary>
/// Aggregates one or more <see cref="ICalendarProvider"/> instances for the Calendar Widget.
/// Keeps Widgets free of provider-specific branching.
/// </summary>
public sealed class CalendarService
{
    private readonly IReadOnlyList<ICalendarProvider> _providers;

    public CalendarService(IEnumerable<ICalendarProvider> providers)
    {
        _providers = providers.ToList();
    }

    public IReadOnlyList<ICalendarProvider> Providers => _providers;

    public async Task<IReadOnlyList<CalendarEvent>> GetAgendaAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default)
    {
        var merged = new List<CalendarEvent>();
        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var events = await provider.GetEventsAsync(query, cancellationToken).ConfigureAwait(false);
                merged.AddRange(events);
            }
            catch
            {
                // Provider failures must not take down the widget — skip this source.
            }
        }

        return merged
            .OrderBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<CalendarEvent>> GetTodayAgendaAsync(
        DateTimeOffset localNow,
        CancellationToken cancellationToken = default) =>
        await GetAgendaAsync(CalendarQuery.ForToday(localNow), cancellationToken).ConfigureAwait(false);
}
