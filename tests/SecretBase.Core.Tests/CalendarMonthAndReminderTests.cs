using SecretBase.Core.Calendar;

namespace SecretBase.Core.Tests;

public class CalendarMonthLayoutTests
{
    [Fact]
    public void Build_PadsSundayFirstAndCountsEvents()
    {
        // 2026-10-01 is Thursday → 4 leading pad days from Sunday Sep 27.
        var events = new List<CalendarEvent>
        {
            new()
            {
                Title = "Study",
                Start = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.FromHours(9)),
                End = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.FromHours(9))
            },
            new()
            {
                Title = "Guitar",
                Start = new DateTimeOffset(2026, 10, 9, 19, 0, 0, TimeSpan.FromHours(9)),
                End = new DateTimeOffset(2026, 10, 9, 20, 0, 0, TimeSpan.FromHours(9))
            }
        };

        var cells = CalendarMonthLayout.Build(
            2026,
            10,
            today: new DateOnly(2026, 10, 9),
            selected: new DateOnly(2026, 10, 9),
            events);

        Assert.Equal(42, cells.Count);
        Assert.Equal(new DateOnly(2026, 9, 27), cells[0].Date);
        Assert.False(cells[0].IsCurrentMonth);

        var oct9 = cells.Single(c => c.Date == new DateOnly(2026, 10, 9));
        Assert.True(oct9.IsCurrentMonth);
        Assert.True(oct9.IsToday);
        Assert.True(oct9.IsSelected);
        Assert.Equal(2, oct9.EventCount);
    }

    [Fact]
    public void ForMonth_CoversFirstThroughLastDay()
    {
        var query = CalendarQuery.ForMonth(new DateOnly(2026, 2, 15));
        Assert.Equal(new DateOnly(2026, 2, 1), query.FromInclusive);
        Assert.Equal(new DateOnly(2026, 2, 28), query.ToInclusive);
    }
}

public class CalendarReminderMonitorTests
{
    [Fact]
    public void CollectDue_FiresOnceWhenEventStarts()
    {
        var monitor = new CalendarReminderMonitor(TimeSpan.FromMinutes(2));
        var start = new DateTimeOffset(2026, 10, 9, 14, 0, 0, TimeSpan.FromHours(9));
        var ev = new CalendarEvent
        {
            Id = "evt1",
            Title = "Meeting",
            Start = start,
            End = start.AddHours(1)
        };

        var first = monitor.CollectDue(start.AddSeconds(30), [ev]);
        Assert.Single(first);
        Assert.Equal("Meeting", first[0].Title);

        var second = monitor.CollectDue(start.AddSeconds(45), [ev]);
        Assert.Empty(second);

        var notice = CalendarReminderMonitor.FormatNotice(first[0]);
        Assert.Contains("14:00", notice, StringComparison.Ordinal);
        Assert.Contains("Meeting", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectDue_RespectsDisabledAndAllDay()
    {
        var monitor = new CalendarReminderMonitor();
        var start = new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero);
        var timed = new CalendarEvent { Id = "t", Title = "A", Start = start, End = start.AddHours(1) };
        var allDay = new CalendarEvent
        {
            Id = "d",
            Title = "Holiday",
            Start = start,
            End = start.AddDays(1),
            IsAllDay = true
        };

        Assert.Empty(monitor.CollectDue(start, [timed], enabled: false));
        Assert.Empty(monitor.CollectDue(start, [allDay], enabled: true));
    }
}

public class CalendarBulkEntryParserTests
{
    [Fact]
    public void TryParse_EnglishRangeAndSlotLines()
    {
        Assert.True(CalendarBulkEntryParser.TryParse(
            "09:00 Study\n10:00-11:30 Guitar\n",
            out var events));
        Assert.Equal(2, events.Count);
        Assert.Equal("Study", events[0].Title);
        Assert.Equal(9, events[0].Hour);
        Assert.Equal(60, events[0].DurationMinutes);
        Assert.Equal("Guitar", events[1].Title);
        Assert.Equal(90, events[1].DurationMinutes);
    }

    [Fact]
    public void TryParse_JapaneseDayPlan()
    {
        Assert.True(CalendarBulkEntryParser.TryParse(
            "19時勉強、20時食事、22:00から23:00まで英語",
            out var events));
        Assert.Equal(3, events.Count);
        Assert.Equal("英語", events[^1].Title);
        Assert.Equal(60, events[^1].DurationMinutes);
    }

    [Fact]
    public void FormatPreview_IncludesRanges()
    {
        var preview = CalendarBulkEntryParser.FormatPreview(
        [
            new("Study", 9, 0, 60)
        ]);
        Assert.Contains("09:00", preview, StringComparison.Ordinal);
        Assert.Contains("10:00", preview, StringComparison.Ordinal);
        Assert.Contains("Study", preview, StringComparison.Ordinal);
    }
}
