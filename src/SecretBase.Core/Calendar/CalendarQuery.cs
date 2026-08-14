namespace SecretBase.Core.Calendar;

/// <summary>Inclusive local-date query window for providers.</summary>
public readonly record struct CalendarQuery(DateOnly FromInclusive, DateOnly ToInclusive)
{
    public static CalendarQuery ForDay(DateOnly day) => new(day, day);

    public static CalendarQuery ForToday(DateTimeOffset localNow) =>
        ForDay(DateOnly.FromDateTime(localNow.DateTime));
}
