namespace SecretBase.Core.Calendar;

/// <summary>Allowed Calendar operations for future AI. Widget UI still uses CalendarService directly.</summary>
public enum CalendarCommandKind
{
    GetTodayEvents = 0,
    Refresh = 1,
    Open = 2,
    GetUpcoming = 3
}

/// <summary>Validated calendar intent. Never talks to Google APIs without CalendarService.</summary>
public sealed class CalendarCommand
{
    public CalendarCommandKind Kind { get; init; }

    /// <summary>Lookahead days for <see cref="CalendarCommandKind.GetUpcoming"/> (1–14).</summary>
    public int Days { get; init; } = 7;

    public static CalendarCommand GetTodayEvents() =>
        new() { Kind = CalendarCommandKind.GetTodayEvents };

    public static CalendarCommand GetUpcoming(int days = 7) =>
        new() { Kind = CalendarCommandKind.GetUpcoming, Days = days };

    public static CalendarCommand Refresh() =>
        new() { Kind = CalendarCommandKind.Refresh };

    public static CalendarCommand Open() =>
        new() { Kind = CalendarCommandKind.Open };
}
