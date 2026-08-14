namespace SecretBase.Core.Calendar;

/// <summary>Sample timed events for first-run demos (local provider).</summary>
public static class SampleCalendarEvents
{
    public static IReadOnlyList<CalendarEvent> CreateCreativeDay(DateOnly day, TimeSpan offset)
    {
        CalendarEvent At(int startHour, int startMinute, int endHour, int endMinute, string title, string color) =>
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Provider = CalendarProviderIds.Local,
                CalendarId = "local",
                CalendarName = "Local",
                Title = title,
                Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(startHour, startMinute)), offset),
                End = new DateTimeOffset(day.ToDateTime(new TimeOnly(endHour, endMinute)), offset),
                IsAllDay = false,
                Color = color,
                Source = "Local",
                LastUpdated = DateTimeOffset.UtcNow
            };

        return
        [
            At(9, 0, 12, 0, "東進", "#E67E22"),
            At(14, 0, 16, 0, "Programming", "#3498DB"),
            At(19, 0, 20, 0, "Guitar", "#9B59B6")
        ];
    }
}
