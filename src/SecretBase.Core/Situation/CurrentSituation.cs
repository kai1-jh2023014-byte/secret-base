using SecretBase.Core.Activity;
using SecretBase.Core.Calendar;
using SecretBase.Core.Focus;
using SecretBase.Core.Memory;
using SecretBase.Core.Session;
using SecretBase.Core.State;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Situation;

public sealed class SituationEvidence
{
    public string Source { get; init; } = string.Empty;

    public string Fact { get; init; } = string.Empty;

    public double Weight { get; init; }
}

/// <summary>Grounded picture of now. Confidence is explained by Evidence — not a diagnosis.</summary>
public sealed class CurrentSituation
{
    public DateTimeOffset At { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectId { get; init; }

    public string Activity { get; init; } = "unknown";

    public string? RecentActivity { get; init; }

    public string? Workspace { get; init; }

    public bool Focus { get; init; }

    public string? Calendar { get; init; }

    public string? NextTask { get; init; }

    public double Confidence { get; init; }

    public IReadOnlyList<SituationEvidence> Evidence { get; init; } = [];

    public string? ActiveApplication { get; init; }

    public string WorkingState { get; init; } = "unknown";

    public string? LikelyIntent { get; set; }

    public string? NextLikelyAction { get; set; }

    public TimeSpan? SessionAge { get; init; }

    public bool ProjectContinuity { get; init; }

    public int InterruptionLevel { get; init; }

    public string Format()
    {
        var lines = new List<string>
        {
            "Project: " + (ProjectName ?? "(none)"),
            "Activity: " + Activity,
            "Workspace: " + (Workspace ?? "(none)"),
            "Focus: " + (Focus ? "on" : "off"),
            "Calendar: " + (Calendar ?? "(none)"),
            "Next: " + (NextTask ?? "(none)"),
            $"Confidence: {Confidence:0.00}"
        };
        foreach (var item in Evidence.Take(6))
        {
            lines.Add($"- [{item.Source}] {item.Fact}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class SituationComposer
{
    public static CurrentSituation Compose(
        UserState state,
        IReadOnlyList<MeaningfulActivity>? recent = null,
        IReadOnlyList<MemoryEntry>? memories = null,
        WorkSession? lastSession = null,
        FocusSession? focus = null,
        IReadOnlyList<CalendarEvent>? events = null,
        TodoList? todos = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        recent ??= [];
        memories ??= [];
        events ??= [];
        todos ??= new TodoList();
        var evidence = new List<SituationEvidence>();
        var last = recent.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(state.CurrentProjectName))
        {
            evidence.Add(new SituationEvidence
            {
                Source = "project",
                Fact = "Current project is " + state.CurrentProjectName,
                Weight = 0.28
            });
        }

        if (last is not null)
        {
            evidence.Add(new SituationEvidence
            {
                Source = "activity",
                Fact = last.Title,
                Weight = 0.22
            });
        }

        if (state.CurrentCalendarTitle is not null)
        {
            evidence.Add(new SituationEvidence
            {
                Source = "calendar",
                Fact = "Calendar block: " + state.CurrentCalendarTitle,
                Weight = 0.24
            });
        }

        if (state.OpenTodoCount > 0 && !string.IsNullOrWhiteSpace(state.ActiveTodo))
        {
            evidence.Add(new SituationEvidence
            {
                Source = "todo",
                Fact = "Unfinished: " + state.ActiveTodo,
                Weight = 0.12
            });
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentWorkspaceTitle))
        {
            evidence.Add(new SituationEvidence
            {
                Source = "workspace",
                Fact = state.CurrentWorkspaceTitle,
                Weight = 0.14
            });
        }

        if (lastSession is not null && !string.IsNullOrWhiteSpace(lastSession.Summary))
        {
            evidence.Add(new SituationEvidence
            {
                Source = "session",
                Fact = "Previous session: " + lastSession.Summary,
                Weight = 0.18
            });
        }

        var sessionMemory = memories.FirstOrDefault(item => item.Scope == MemoryScope.Session);
        if (sessionMemory is not null)
        {
            evidence.Add(new SituationEvidence
            {
                Source = "memory",
                Fact = sessionMemory.Summary,
                Weight = 0.1
            });
        }

        var focusOn = focus is { IsRunning: true } && !focus.IsComplete(state.Now);
        if (focusOn)
        {
            evidence.Add(new SituationEvidence { Source = "focus", Fact = "Focus timer is running", Weight = 0.2 });
        }

        var idle = last is not null && last.Kinds.Contains(ActivityKind.IdleStarted);
        var activity = focusOn
            ? "focus"
            : idle
                ? "idle"
                : !string.IsNullOrWhiteSpace(state.CurrentProjectName) || last is { ProjectName: not null }
                    ? "coding"
                    : !string.IsNullOrWhiteSpace(state.MusicLine)
                        ? "listening"
                        : "unknown";

        var confidence = Math.Clamp(evidence.Sum(item => item.Weight), 0.15, 0.97);
        var projectName = state.CurrentProjectName ?? last?.ProjectName ?? lastSession?.ProjectName;
        var continuity = lastSession is not null
                         && !string.IsNullOrWhiteSpace(projectName)
                         && string.Equals(lastSession.ProjectName, projectName, StringComparison.OrdinalIgnoreCase);
        var working = focusOn
            ? "focus"
            : activity == "idle"
                ? "idle"
                : activity == "listening"
                    ? "break"
                    : activity == "unknown"
                        ? "unknown"
                        : "working";
        return new CurrentSituation
        {
            At = state.Now,
            ProjectName = projectName,
            ProjectId = state.CurrentProjectId ?? lastSession?.ProjectId,
            Activity = activity,
            RecentActivity = last?.Title ?? lastSession?.PrimaryActivity,
            Workspace = state.CurrentWorkspaceTitle ?? lastSession?.WorkspaceTitle,
            Focus = focusOn,
            Calendar = state.CurrentCalendarTitle ?? state.UpcomingCalendarTitle,
            NextTask = state.ActiveTodo ?? lastSession?.UnfinishedTasks.FirstOrDefault(),
            Confidence = Math.Round(confidence, 2),
            Evidence = evidence
                .OrderByDescending(item => item.Weight)
                .Take(8)
                .ToList(),
            ActiveApplication = last?.Title,
            WorkingState = working,
            SessionAge = lastSession is { EndedAt: null } ? state.Now - lastSession.StartedAt : null,
            ProjectContinuity = continuity,
            InterruptionLevel = focusOn ? 0 : working == "idle" ? 1 : 2
        };
    }
}
