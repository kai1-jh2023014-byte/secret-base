namespace SecretBase.Core.Calendar;

/// <summary>Allowed Calendar operations for UI and AI. Widget UI still uses CalendarService directly.</summary>
public enum CalendarCommandKind
{
    GetTodayEvents = 0,
    Refresh = 1,
    Open = 2,
    GetUpcoming = 3,
    AddEvent = 4,
    RememberUsual = 5,
    ApplyUsual = 6,
    RemoveEvent = 7
}

/// <summary>Validated calendar intent. Never talks to Google APIs without CalendarService.</summary>
public sealed class CalendarCommand
{
    public CalendarCommandKind Kind { get; init; }

    /// <summary>Lookahead days for <see cref="CalendarCommandKind.GetUpcoming"/> (1–14).</summary>
    public int Days { get; init; } = 7;

    public string? Title { get; init; }

    /// <summary>Hour 0–23. -1 means use the current hour.</summary>
    public int Hour { get; init; } = -1;

    public int Minute { get; init; }

    public int DurationMinutes { get; init; } = 60;

    public bool IsAllDay { get; init; }

    public string? EventId { get; init; }

    public static CalendarCommand GetTodayEvents() =>
        new() { Kind = CalendarCommandKind.GetTodayEvents };

    public static CalendarCommand GetUpcoming(int days = 7) =>
        new() { Kind = CalendarCommandKind.GetUpcoming, Days = days };

    public static CalendarCommand Refresh() =>
        new() { Kind = CalendarCommandKind.Refresh };

    public static CalendarCommand Open() =>
        new() { Kind = CalendarCommandKind.Open };

    public static CalendarCommand AddEvent(
        string title,
        int hour = -1,
        int minute = 0,
        int durationMinutes = 60,
        bool isAllDay = false) =>
        new()
        {
            Kind = CalendarCommandKind.AddEvent,
            Title = title,
            Hour = hour,
            Minute = minute,
            DurationMinutes = durationMinutes,
            IsAllDay = isAllDay
        };

    public static CalendarCommand RememberUsual(
        string title,
        int hour,
        int minute,
        int durationMinutes = 60,
        bool isAllDay = false) =>
        new()
        {
            Kind = CalendarCommandKind.RememberUsual,
            Title = title,
            Hour = hour,
            Minute = minute,
            DurationMinutes = durationMinutes,
            IsAllDay = isAllDay
        };

    public static CalendarCommand ApplyUsual() =>
        new() { Kind = CalendarCommandKind.ApplyUsual };

    public static CalendarCommand RemoveEvent(string eventId) =>
        new() { Kind = CalendarCommandKind.RemoveEvent, EventId = eventId };
}
