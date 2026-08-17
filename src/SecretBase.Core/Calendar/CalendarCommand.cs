namespace SecretBase.Core.Calendar;

/// <summary>Allowed Calendar operations for future AI. Widget UI still uses CalendarService directly.</summary>
public enum CalendarCommandKind
{
    GetTodayEvents = 0,
    Refresh = 1,
    Open = 2
}

/// <summary>Validated calendar intent. Never talks to Google APIs without CalendarService.</summary>
public sealed class CalendarCommand
{
    public CalendarCommandKind Kind { get; init; }

    public static CalendarCommand GetTodayEvents() =>
        new() { Kind = CalendarCommandKind.GetTodayEvents };

    public static CalendarCommand Refresh() =>
        new() { Kind = CalendarCommandKind.Refresh };

    public static CalendarCommand Open() =>
        new() { Kind = CalendarCommandKind.Open };
}
