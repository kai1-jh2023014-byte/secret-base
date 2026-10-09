namespace SecretBase.Core.Calendar;

/// <summary>Builds a Sunday-first month grid for the Calendar widget (pure Core, no UI).</summary>
public static class CalendarMonthLayout
{
    public readonly record struct Cell(
        DateOnly Date,
        bool IsCurrentMonth,
        bool IsToday,
        bool IsSelected,
        int EventCount);

    public static IReadOnlyList<Cell> Build(
        int year,
        int month,
        DateOnly today,
        DateOnly selected,
        IReadOnlyList<CalendarEvent> monthEvents)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var first = new DateOnly(year, month, 1);
        // Sunday-first: DateTime.DayOfWeek Sunday = 0.
        var pad = (int)first.DayOfWeek;
        var gridStart = first.AddDays(-pad);
        var counts = CountEventsByDay(monthEvents);

        var cells = new List<Cell>(42);
        for (var i = 0; i < 42; i++)
        {
            var day = gridStart.AddDays(i);
            counts.TryGetValue(day, out var count);
            cells.Add(new Cell(
                Date: day,
                IsCurrentMonth: day.Month == month && day.Year == year,
                IsToday: day == today,
                IsSelected: day == selected,
                EventCount: count));
        }

        return cells;
    }

    public static string FormatMonthTitle(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);

    private static Dictionary<DateOnly, int> CountEventsByDay(IReadOnlyList<CalendarEvent> events)
    {
        var map = new Dictionary<DateOnly, int>();
        if (events.Count == 0)
        {
            return map;
        }

        foreach (var ev in events)
        {
            var start = new DateOnly(ev.Start.Year, ev.Start.Month, ev.Start.Day);
            var end = new DateOnly(ev.End.Year, ev.End.Month, ev.End.Day);
            if (ev.IsAllDay && ev.End > ev.Start
                && ev.End.Hour == 0 && ev.End.Minute == 0 && ev.End.Second == 0)
            {
                end = end.AddDays(-1);
            }

            if (end < start)
            {
                end = start;
            }

            for (var d = start; d <= end; d = d.AddDays(1))
            {
                map[d] = map.TryGetValue(d, out var n) ? n + 1 : 1;
            }
        }

        return map;
    }
}
