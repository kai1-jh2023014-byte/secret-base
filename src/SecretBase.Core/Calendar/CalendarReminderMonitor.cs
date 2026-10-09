namespace SecretBase.Core.Calendar;

/// <summary>
/// Detects timed events that should fire a start notification.
/// Pure Core — hosts decide how to surface the notice (toast, status line, etc.).
/// </summary>
public sealed class CalendarReminderMonitor
{
    private readonly HashSet<string> _notifiedKeys = new(StringComparer.Ordinal);
    private readonly TimeSpan _graceAfterStart;

    public CalendarReminderMonitor(TimeSpan? graceAfterStart = null)
    {
        _graceAfterStart = graceAfterStart ?? TimeSpan.FromMinutes(2);
    }

    public readonly record struct Reminder(string EventId, string Title, DateTimeOffset Start, DateTimeOffset End);

    /// <summary>
    /// Returns reminders for events whose start falls in
    /// [now − lead, now − lead + grace] (lead usually 0 = at start).
    /// </summary>
    public IReadOnlyList<Reminder> CollectDue(
        DateTimeOffset localNow,
        IEnumerable<CalendarEvent> events,
        int leadMinutes = 0,
        bool enabled = true)
    {
        if (!enabled)
        {
            return [];
        }

        var lead = TimeSpan.FromMinutes(Math.Clamp(leadMinutes, 0, 120));
        var due = new List<Reminder>();

        foreach (var ev in events)
        {
            if (ev.IsAllDay || string.IsNullOrWhiteSpace(ev.Title))
            {
                continue;
            }

            var triggerAt = ev.Start - lead;
            if (localNow < triggerAt || localNow > triggerAt + _graceAfterStart)
            {
                continue;
            }

            var key = ReminderKey(ev.Id, triggerAt);
            if (!_notifiedKeys.Add(key))
            {
                continue;
            }

            due.Add(new Reminder(ev.Id, ev.Title.Trim(), ev.Start, ev.End));
        }

        return due;
    }

    public void Forget(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        _notifiedKeys.RemoveWhere(k => k.StartsWith(eventId + "|", StringComparison.Ordinal));
    }

    public void Reset() => _notifiedKeys.Clear();

    public static string FormatNotice(Reminder reminder)
    {
        var start = reminder.Start.DateTime;
        var end = reminder.End.DateTime;
        var range = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{start:HH:mm} 〜 {end:HH:mm}");
        return $"Starting now · {range} · {reminder.Title}";
    }

    private static string ReminderKey(string eventId, DateTimeOffset triggerAt) =>
        $"{eventId}|{triggerAt.UtcTicks}";
}
