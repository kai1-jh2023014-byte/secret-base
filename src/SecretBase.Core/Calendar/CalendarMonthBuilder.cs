namespace SecretBase.Core.Calendar;

/// <summary>Pure month-grid builder — no UI / OS dependencies.</summary>
public static class CalendarMonthBuilder
{
    public const int WeeksInGrid = 6;
    public const int DaysInWeek = 7;
    public const int CellsInGrid = WeeksInGrid * DaysInWeek;

    /// <summary>
    /// Builds a fixed 6×7 grid for <paramref name="year"/>/<paramref name="month"/>.
    /// Cells may include days from the previous/next month for alignment.
    /// </summary>
    public static IReadOnlyList<CalendarDayCell> Build(
        int year,
        int month,
        DayOfWeek firstDayOfWeek,
        DateOnly today,
        ICalendarEventSource? eventSource = null)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        var source = eventSource ?? EmptyCalendarEventSource.Instance;
        var firstOfMonth = new DateOnly(year, month, 1);
        var start = AlignToWeekStart(firstOfMonth, firstDayOfWeek);
        var end = start.AddDays(CellsInGrid - 1);

        var events = source.GetEvents(start, end);
        var byDate = events
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<CalendarEvent>)g.ToList());

        var cells = new List<CalendarDayCell>(CellsInGrid);
        for (var i = 0; i < CellsInGrid; i++)
        {
            var date = start.AddDays(i);
            byDate.TryGetValue(date, out var dayEvents);
            cells.Add(new CalendarDayCell
            {
                Date = date,
                IsCurrentMonth = date.Month == month && date.Year == year,
                IsToday = date == today,
                Events = dayEvents ?? Array.Empty<CalendarEvent>()
            });
        }

        return cells;
    }

    public static DateOnly AlignToWeekStart(DateOnly date, DayOfWeek firstDayOfWeek)
    {
        var delta = ((int)date.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return date.AddDays(-delta);
    }

    /// <summary>Short weekday labels starting at <paramref name="firstDayOfWeek"/> (Sun…Sat English).</summary>
    public static IReadOnlyList<string> WeekdayLabels(DayOfWeek firstDayOfWeek)
    {
        string[] names = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
        var labels = new string[DaysInWeek];
        for (var i = 0; i < DaysInWeek; i++)
        {
            labels[i] = names[((int)firstDayOfWeek + i) % 7];
        }

        return labels;
    }
}
