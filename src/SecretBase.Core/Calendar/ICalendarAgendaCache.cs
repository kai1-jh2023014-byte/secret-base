namespace SecretBase.Core.Calendar;

/// <summary>Optional last-success agenda cache (events only — never tokens).</summary>
public interface ICalendarAgendaCache
{
    void Save(IReadOnlyList<CalendarEvent> events, DateTimeOffset savedAt);

    IReadOnlyList<CalendarEvent>? TryLoad(out DateTimeOffset? savedAt);
}
