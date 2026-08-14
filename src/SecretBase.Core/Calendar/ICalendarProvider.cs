namespace SecretBase.Core.Calendar;

/// <summary>
/// Provider-agnostic calendar integration. Map external calendars into
/// <see cref="CalendarEvent"/>. Network I/O stays in Infrastructure/Platform.
/// </summary>
public interface ICalendarProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    string? OpenUrl { get; }

    CalendarProviderCapabilities Capabilities { get; }

    CalendarAuthStatus AuthStatus { get; }

    bool IsConfigured { get; }

    Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Starts provider auth when <see cref="CalendarProviderCapabilities.Authentication"/> is set.</summary>
    Task AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>Clears stored credentials when authentication is supported.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
