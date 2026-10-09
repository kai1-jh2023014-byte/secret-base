namespace SecretBase.Core.Calendar;

/// <summary>Inclusive local-date query window for providers.</summary>
public readonly record struct CalendarQuery(DateOnly FromInclusive, DateOnly ToInclusive)
{
    public static CalendarQuery ForDay(DateOnly day) => new(day, day);

    public static CalendarQuery ForToday(DateTimeOffset localNow) =>
        ForDay(ToCalendarDate(localNow));

    /// <summary>Today through today+days (inclusive), clamped 1–14.</summary>
    public static CalendarQuery ForUpcoming(DateTimeOffset localNow, int days)
    {
        var span = Math.Clamp(days, 1, 14);
        var start = ToCalendarDate(localNow);
        return new CalendarQuery(start, start.AddDays(span));
    }

    /// <summary>Full local calendar month containing <paramref name="anyDayInMonth"/>.</summary>
    public static CalendarQuery ForMonth(DateOnly anyDayInMonth)
    {
        var first = new DateOnly(anyDayInMonth.Year, anyDayInMonth.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return new CalendarQuery(first, last);
    }

    /// <summary>Calendar date in <paramref name="instant"/>'s own offset (not machine local TZ).</summary>
    internal static DateOnly ToCalendarDate(DateTimeOffset instant) =>
        new(instant.Year, instant.Month, instant.Day);
}
