namespace SecretBase.Core.Calendar;

/// <summary>Sample timed events for first-run / empty Google ICS demos (local provider).</summary>
public static class SampleCalendarEvents
{
    public static IReadOnlyList<CalendarEvent> CreateCreativeDay(DateOnly day, TimeSpan offset)
    {
        CalendarEvent At(int startHour, int startMinute, int endHour, int endMinute, string title) =>
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Provider = CalendarProviderIds.Local,
                CalendarName = "Local",
                Title = title,
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(startHour, startMinute)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(endHour, endMinute)), offset),
                IsAllDay = false
            };

        return
        [
            At(9, 0, 12, 0, "東進"),
            At(14, 0, 16, 0, "Programming"),
            At(19, 0, 20, 0, "Guitar")
        ];
    }
}
