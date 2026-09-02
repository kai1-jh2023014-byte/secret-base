using SecretBase.Core.Calendar;

namespace SecretBase.Core.Tests;

public class CalendarQueryTimezoneTests
{
    [Fact]
    public void ForToday_UsesOffsetCalendarDate_NotMachineLocalTimezone()
    {
        // 2026-08-24 09:00 +09:00 must query Aug 24 even when machine TZ is US/Pacific.
        var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        var query = CalendarQuery.ForToday(now);
        Assert.Equal(new DateOnly(2026, 8, 24), query.FromInclusive);
        Assert.Equal(new DateOnly(2026, 8, 24), query.ToInclusive);
    }

    [Fact]
    public void ForUpcoming_UsesOffsetCalendarDate_NotMachineLocalTimezone()
    {
        var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        var query = CalendarQuery.ForUpcoming(now, days: 3);
        Assert.Equal(new DateOnly(2026, 8, 24), query.FromInclusive);
        Assert.Equal(new DateOnly(2026, 8, 27), query.ToInclusive);
    }
}
