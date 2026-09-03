namespace SecretBase.Core.Calendar;

/// <summary>
/// Persists Secret Base local events and the remembered usual schedule.
/// Does not talk to Google. Never deletes OS files.
/// </summary>
public interface ILocalCalendarStore
{
    IReadOnlyList<CalendarEvent> LoadEvents();

    void SaveEvents(IReadOnlyList<CalendarEvent> events);

    IReadOnlyList<UsualScheduleSlot> LoadUsual();

    void SaveUsual(IReadOnlyList<UsualScheduleSlot> slots);
}
