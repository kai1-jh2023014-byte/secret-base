using SecretBase.Core.Activity;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Focus;
using SecretBase.Core.Memory;
using SecretBase.Core.Music;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.State;

/// <summary>Live picture of the user in Secret Base. Confidence is heuristic, not a diagnosis.</summary>
public sealed class UserState
{
    public DateTimeOffset Now { get; init; }

    public string Greeting { get; init; } = string.Empty;

    public string? CurrentProjectName { get; init; }

    public string? CurrentProjectId { get; init; }

    public string? CurrentWorkspaceTitle { get; init; }

    public bool FocusRunning { get; init; }

    public string FocusLine { get; init; } = string.Empty;

    public string? CurrentCalendarTitle { get; init; }

    public string? UpcomingCalendarTitle { get; init; }

    public TimeSpan? UntilUpcoming { get; init; }

    public string? ActiveTodo { get; init; }

    public int OpenTodoCount { get; init; }

    public string RecentActivityLine { get; init; } = string.Empty;

    public string MusicLine { get; init; } = string.Empty;

    public string ProviderLine { get; init; } = string.Empty;

    public bool Available { get; init; } = true;

    public double Confidence { get; init; }
}

public static class BaseGreeting
{
    public static string For(DateTimeOffset now) =>
        now.Hour switch
        {
            >= 5 and < 12 => "Good morning",
            >= 12 and < 18 => "Good afternoon",
            _ => "Good evening"
        };
}

public static class UserStateComposer
{
    public static UserState Compose(
        DateTimeOffset now,
        WorkspaceSession? workspace,
        FocusSession? focus,
        IReadOnlyList<CalendarEvent> events,
        TodoList? todos,
        IReadOnlyList<MeaningfulActivity> recent,
        AssistantMusicState? music,
        AssistantProviderStatusInfo? provider,
        IReadOnlyList<MemoryEntry>? memories = null)
    {
        events ??= [];
        todos ??= new TodoList();
        recent ??= [];
        memories ??= [];

        var current = events
            .Where(item => !item.IsAllDay && item.Start <= now && item.End > now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        var upcoming = events
            .Where(item => !item.IsAllDay && item.Start > now)
            .OrderBy(item => item.Start)
            .FirstOrDefault();
        var openTodos = todos.Items.Where(item => !item.IsDone).ToList();
        var last = recent.LastOrDefault();
        var projectMemory = memories.FirstOrDefault(item => item.Scope == MemoryScope.Project);
        var projectName = workspace?.ProjectName
                          ?? last?.ProjectName
                          ?? projectMemory?.ProjectName;
        var projectId = workspace?.ProjectId ?? projectMemory?.ProjectId;
        var focusRunning = focus is { IsRunning: true } && !focus.IsComplete(now);
        var signals = 0;
        if (workspace is not null)
        {
            signals++;
        }

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            signals++;
        }

        if (current is not null)
        {
            signals++;
        }

        if (last is not null)
        {
            signals++;
        }

        var confidence = Math.Clamp(0.35 + (signals * 0.12), 0.2, 0.95);
        if (focusRunning)
        {
            confidence = Math.Min(0.99, confidence + 0.08);
        }

        return new UserState
        {
            Now = now,
            Greeting = BaseGreeting.For(now),
            CurrentProjectName = projectName,
            CurrentProjectId = projectId,
            CurrentWorkspaceTitle = workspace?.StatusLine,
            FocusRunning = focusRunning,
            FocusLine = focus?.StatusLine(now) ?? "Focus idle",
            CurrentCalendarTitle = current?.Title,
            UpcomingCalendarTitle = upcoming?.Title,
            UntilUpcoming = upcoming is null ? null : upcoming.Start - now,
            ActiveTodo = openTodos.FirstOrDefault()?.Title,
            OpenTodoCount = openTodos.Count,
            RecentActivityLine = last is null ? string.Empty : last.Title,
            MusicLine = string.IsNullOrWhiteSpace(music?.CurrentTrackTitle)
                ? string.Empty
                : $"{music.CurrentTrackTitle} — {music.CurrentTrackArtist}",
            ProviderLine = BaseAiStatusFormatter.Format(provider),
            Available = !focusRunning,
            Confidence = Math.Round(confidence, 2)
        };
    }
}
