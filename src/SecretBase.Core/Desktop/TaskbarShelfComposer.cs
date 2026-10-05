using System.Globalization;
using SecretBase.Core.Calendar;
using SecretBase.Core.Focus;
using SecretBase.Core.Todo;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Compact facts for the overlay shelf that sits just above the Windows taskbar.
/// The shelf does not replace the taskbar; it only fills more of the work-area edge.
/// Clock/time lives on the Clock widget — not duplicated here.
/// </summary>
public sealed record TaskbarShelfSnapshot(
    string Focus,
    string Next);

public static class TaskbarShelfComposer
{
    public const int NextMaxLength = 36;

    public static TaskbarShelfSnapshot Compose(
        DateTimeOffset now,
        FocusSession? focus,
        IReadOnlyList<TodoItem>? openTodos,
        IReadOnlyList<CalendarEvent>? events)
    {
        openTodos ??= [];
        events ??= [];

        var focusLine = focus is { IsRunning: true }
            ? focus.StatusLine(now)
            : "Focus idle";

        return new TaskbarShelfSnapshot(focusLine, FormatNext(now, openTodos, events));
    }

    private static string FormatNext(
        DateTimeOffset now,
        IReadOnlyList<TodoItem> openTodos,
        IReadOnlyList<CalendarEvent> events)
    {
        var nextEvent = events
            .Where(item => item.Start >= now.AddMinutes(-1))
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        var nextTodo = openTodos.FirstOrDefault(item => !item.IsDone && !string.IsNullOrWhiteSpace(item.Title));

        if (nextEvent is not null && (nextTodo is null || nextEvent.Start <= now.AddHours(4)))
        {
            var title = Trim(nextEvent.Title);
            var until = nextEvent.Start - now;
            if (until <= TimeSpan.FromMinutes(1))
            {
                return $"Now {title}";
            }

            return nextEvent.Start.ToString("HH:mm", CultureInfo.InvariantCulture) + " " + title;
        }

        if (nextTodo is not null)
        {
            return Trim(nextTodo.Title);
        }

        return "Nothing queued";
    }

    private static string Trim(string? value)
    {
        var text = (value ?? string.Empty).Trim().Replace('\n', ' ');
        if (text.Length <= NextMaxLength)
        {
            return text;
        }

        return text[..(NextMaxLength - 1)] + "…";
    }
}
