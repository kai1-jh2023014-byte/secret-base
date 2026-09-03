namespace SecretBase.Core.Calendar;

/// <summary>Result of a CalendarCommand. Host opens URLs after validation.</summary>
public sealed class CalendarCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public CalendarCommandKind Kind { get; init; }

    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public UsualScheduleSlot? Usual { get; init; }

    public static CalendarCommandResult Ok(
        CalendarCommandKind kind,
        IReadOnlyList<CalendarEvent>? events = null,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        UsualScheduleSlot? usual = null) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Events = events ?? Array.Empty<CalendarEvent>(),
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            Usual = usual
        };

    public static CalendarCommandResult Fail(CalendarCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
