using SecretBase.Core.Calendar;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Calendar;

namespace SecretBase.Core.Tests;

public class CalendarMonthBuilderTests
{
    [Fact]
    public void Build_ReturnsFixedSixBySevenGrid()
    {
        var today = new DateOnly(2026, 8, 13);
        var cells = CalendarMonthBuilder.Build(2026, 8, DayOfWeek.Monday, today);

        Assert.Equal(CalendarMonthBuilder.CellsInGrid, cells.Count);
        Assert.Contains(cells, c => c.IsToday && c.Date == today);
        Assert.Equal(31, cells.Count(c => c.IsCurrentMonth));
    }

    [Fact]
    public void Build_AlignsFirstCellToFirstDayOfWeek()
    {
        var cells = CalendarMonthBuilder.Build(
            2026,
            8,
            DayOfWeek.Monday,
            today: new DateOnly(2026, 8, 13));

        Assert.Equal(DayOfWeek.Monday, cells[0].Date.DayOfWeek);
    }

    [Fact]
    public void Build_AttachesEventsFromSource()
    {
        var source = new LocalCalendarEventSource(
        [
            new CalendarEvent { Date = new DateOnly(2026, 8, 13), Title = "Ship Calendar" },
            new CalendarEvent { Date = new DateOnly(2026, 8, 20), Title = "Review" }
        ]);

        var cells = CalendarMonthBuilder.Build(
            2026,
            8,
            DayOfWeek.Monday,
            today: new DateOnly(2026, 8, 1),
            source);

        var day = cells.Single(c => c.Date == new DateOnly(2026, 8, 13));
        Assert.True(day.HasEvents);
        Assert.Equal("Ship Calendar", day.Events[0].Title);
    }

    [Fact]
    public void WeekdayLabels_StartAtConfiguredDay()
    {
        var labels = CalendarMonthBuilder.WeekdayLabels(DayOfWeek.Monday);
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], labels);
    }
}

public class LocalCalendarEventSourceTests
{
    [Fact]
    public void GetEvents_FiltersInclusiveRangeAndOrders()
    {
        var source = new LocalCalendarEventSource(
        [
            new CalendarEvent { Date = new DateOnly(2026, 8, 20), Title = "B" },
            new CalendarEvent { Date = new DateOnly(2026, 8, 10), Title = "A" },
            new CalendarEvent { Date = new DateOnly(2026, 9, 1), Title = "C" }
        ]);

        var events = source.GetEvents(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));
        Assert.Equal(2, events.Count);
        Assert.Equal("A", events[0].Title);
        Assert.Equal("B", events[1].Title);
    }

    [Fact]
    public void EmptySource_ReturnsNoEvents()
    {
        Assert.Empty(EmptyCalendarEventSource.Instance.GetEvents(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31)));
    }
}

public class CalendarWidgetConfigurationTests
{
    [Fact]
    public void RoundTrip_PreservesEventsAndFirstDay()
    {
        var original = new CalendarWidgetConfiguration
        {
            FirstDayOfWeek = DayOfWeek.Sunday,
            FollowToday = false,
            PinnedYear = 2026,
            PinnedMonth = 8,
            Events =
            [
                new CalendarEvent
                {
                    Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                    Date = new DateOnly(2026, 8, 13),
                    Title = "Desk day",
                    Notes = "local only"
                }
            ]
        };

        var restored = CalendarWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal(DayOfWeek.Sunday, restored.FirstDayOfWeek);
        Assert.False(restored.FollowToday);
        Assert.Equal(2026, restored.PinnedYear);
        Assert.Equal(8, restored.PinnedMonth);
        Assert.Single(restored.Events);
        Assert.Equal(original.Events[0].Id, restored.Events[0].Id);
        Assert.Equal(new DateOnly(2026, 8, 13), restored.Events[0].Date);
        Assert.Equal("Desk day", restored.Events[0].Title);
        Assert.Equal("local only", restored.Events[0].Notes);
    }

    [Fact]
    public void FromDictionary_MissingKeys_UsesDefaults()
    {
        var restored = CalendarWidgetConfiguration.FromDictionary(
            new Dictionary<string, System.Text.Json.JsonElement>());
        Assert.Equal(DayOfWeek.Monday, restored.FirstDayOfWeek);
        Assert.True(restored.FollowToday);
        Assert.Empty(restored.Events);
    }

    [Fact]
    public void CreateEventSource_Empty_UsesEmptyProvider()
    {
        var config = CalendarWidgetConfiguration.CreateDefault();
        Assert.Equal("empty", config.CreateEventSource().SourceId);
    }

    [Fact]
    public void CreateCalendar_SetsTypeWithoutTouchingDefaultLayoutSeed()
    {
        var widget = DefaultWidgetFactory.CreateCalendar();
        Assert.Equal(WidgetTypes.Calendar, widget.Type);
        Assert.Equal(320, widget.Size.Width);
        Assert.Equal(340, widget.Size.Height);

        var layout = SecretBase.Core.Desktop.DesktopLayout.CreateDefault();
        Assert.Equal(2, layout.Widgets.Count);
        Assert.DoesNotContain(layout.Widgets, w => w.Type == WidgetTypes.Calendar);
    }
}
