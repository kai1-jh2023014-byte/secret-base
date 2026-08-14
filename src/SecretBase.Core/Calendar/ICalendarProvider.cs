namespace SecretBase.Core.Calendar;

/// <summary>
/// Provider-agnostic calendar integration. Implementations map external calendars
/// into <see cref="CalendarEvent"/>. Network I/O belongs in Infrastructure/Platform —
/// Core may host local/in-memory providers only.
/// </summary>
public interface ICalendarProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    /// <summary>Optional deep-link / web URL for "Open Calendar".</summary>
    string? OpenUrl { get; }

    bool IsConfigured { get; }

    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default);
}
