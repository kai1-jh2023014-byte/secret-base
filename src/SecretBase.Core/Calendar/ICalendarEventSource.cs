namespace SecretBase.Core.Calendar;

/// <summary>
/// Provider-agnostic calendar integration surface.
/// v0.1 ships a local configuration-backed source only.
/// Future Outlook / Google providers must live behind this interface
/// (Platform/Infrastructure), never inside Core UI types.
/// </summary>
public interface ICalendarEventSource
{
    string SourceId { get; }

    /// <summary>
    /// Returns events whose <see cref="CalendarEvent.Date"/> falls in
    /// <paramref name="fromInclusive"/>..<paramref name="toInclusive"/> (inclusive).
    /// </summary>
    IReadOnlyList<CalendarEvent> GetEvents(DateOnly fromInclusive, DateOnly toInclusive);
}
