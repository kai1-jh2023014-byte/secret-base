using SecretBase.Core.Activity;
using SecretBase.Core.Calendar;
using SecretBase.Core.Session;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Timeline;

public sealed class TimelineEntry
{
    public DateTimeOffset At { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Kind { get; init; } = "activity";

    public string? ProjectName { get; init; }
}

/// <summary>Meaningful day view. Raw events are aggregated first.</summary>
public static class ActivityTimeline
{
    public static IReadOnlyList<TimelineEntry> ForDay(
        DateTimeOffset day,
        IReadOnlyList<MeaningfulActivity> activities,
        IReadOnlyList<CalendarEvent>? events = null,
        IReadOnlyList<WorkSession>? sessions = null,
        TodoList? todos = null)
    {
        activities ??= [];
        events ??= [];
        sessions ??= [];
        var start = new DateTimeOffset(day.Date, day.Offset);
        var end = start.AddDays(1);
        var rows = new List<TimelineEntry>();
        foreach (var item in activities.Where(a => a.EndedAt >= start && a.StartedAt < end))
        {
            rows.Add(new TimelineEntry
            {
                At = item.StartedAt,
                Title = item.Title,
                Kind = "activity",
                ProjectName = item.ProjectName
            });
        }

        foreach (var item in events.Where(e => e.Start >= start && e.Start < end))
        {
            rows.Add(new TimelineEntry
            {
                At = item.Start,
                Title = item.Title,
                Kind = "calendar"
            });
        }

        foreach (var item in sessions.Where(s => s.StartedAt >= start && s.StartedAt < end))
        {
            rows.Add(new TimelineEntry
            {
                At = item.StartedAt,
                Title = string.IsNullOrWhiteSpace(item.Summary) ? "Session" : item.Summary,
                Kind = "session",
                ProjectName = item.ProjectName
            });
        }

        if (todos is not null)
        {
            foreach (var item in todos.Items.Where(t => t.IsDone && t.CreatedAt >= start && t.CreatedAt < end))
            {
                rows.Add(new TimelineEntry
                {
                    At = item.CreatedAt,
                    Title = item.Title,
                    Kind = "todo",
                });
            }
        }

        return rows
            .OrderBy(row => row.At)
            .GroupBy(row => row.Kind + "|" + row.Title + "|" + row.At.ToString("HH:mm"), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    public static string Format(IReadOnlyList<TimelineEntry> entries)
    {
        if (entries.Count == 0)
        {
            return "No meaningful activity recorded for that day.";
        }

        return string.Join(
            Environment.NewLine,
            entries.Select(item => $"{item.At:HH:mm}  {item.Title}"));
    }
}
