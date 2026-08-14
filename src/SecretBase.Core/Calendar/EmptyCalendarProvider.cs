namespace SecretBase.Core.Calendar;

/// <summary>No-op provider used when nothing is configured.</summary>
public sealed class EmptyCalendarProvider : ICalendarProvider
{
    public static EmptyCalendarProvider Instance { get; } = new();

    public string ProviderId => "empty";

    public string DisplayName => "None";

    public string? OpenUrl => null;

    public CalendarProviderCapabilities Capabilities => CalendarProviderCapabilities.None;

    public CalendarAuthStatus AuthStatus => CalendarAuthStatus.NotApplicable;

    public bool IsConfigured => false;

    public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarInfo>>(Array.Empty<CalendarInfo>());

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarEvent>>(Array.Empty<CalendarEvent>());

    public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
