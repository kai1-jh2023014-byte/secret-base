using SecretBase.Core.Calendar;
using SecretBase.Core.Explain;
using SecretBase.Core.Intent;
using SecretBase.Core.Session;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Briefing;

public sealed class DailyBriefingSnapshot
{
    public string Greeting { get; init; } = string.Empty;

    public string Headline { get; init; } = string.Empty;

    public IReadOnlyList<string> CalendarLines { get; init; } = [];

    public IReadOnlyList<string> TaskLines { get; init; } = [];

    public string ContinueTitle { get; init; } = string.Empty;

    public string LastSession { get; init; } = string.Empty;

    public string NextStep { get; init; } = string.Empty;

    public string Why { get; init; } = string.Empty;

    public bool ShowContinue { get; init; }

    public string Format()
    {
        var lines = new List<string> { Greeting.ToUpperInvariant(), string.Empty, "Today", "──────────────" };
        if (CalendarLines.Count == 0)
        {
            lines.Add("Calendar  Quiet");
        }
        else
        {
            lines.Add("Calendar");
            lines.AddRange(CalendarLines);
        }

        lines.Add(string.Empty);
        lines.Add("Tasks");
        if (TaskLines.Count == 0)
        {
            lines.Add("Clear");
        }
        else
        {
            lines.AddRange(TaskLines);
        }

        if (!string.IsNullOrWhiteSpace(ContinueTitle))
        {
            lines.Add(string.Empty);
            lines.Add("Continue");
            lines.Add("──────────────");
            if (!string.IsNullOrWhiteSpace(LastSession))
            {
                lines.Add("Yesterday you were working on:");
                lines.Add(ContinueTitle);
                lines.Add(LastSession);
            }

            if (!string.IsNullOrWhiteSpace(NextStep))
            {
                lines.Add("Likely next step:");
                lines.Add(NextStep);
            }

            if (!string.IsNullOrWhiteSpace(Why))
            {
                lines.Add(string.Empty);
                lines.Add(Why);
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public static class DailyBriefingComposer
{
    public static DailyBriefingSnapshot Compose(
        UserState state,
        CurrentSituation situation,
        DetectedIntent intent,
        ProjectContinuationContext continuation,
        IReadOnlyList<CalendarEvent> events,
        TodoList todos,
        WorkSession? lastSession = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(situation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(continuation);
        events ??= [];
        todos ??= new TodoList();
        var day = new DateTimeOffset(state.Now.Date, state.Now.Offset);
        var calendar = events
            .Where(item => item.Start.Date == day.Date)
            .OrderBy(item => item.Start)
            .Take(6)
            .Select(item => $"{item.Start:HH:mm} {item.Title}")
            .ToList();
        var tasks = todos.Items.Where(item => !item.IsDone).Take(6).Select(item => "□ " + item.Title).ToList();
        var project = continuation.ProjectName ?? situation.ProjectName;
        return new DailyBriefingSnapshot
        {
            Greeting = state.Greeting,
            Headline = situation.Calendar ?? project ?? "Your Base is ready.",
            CalendarLines = calendar,
            TaskLines = tasks,
            ContinueTitle = project ?? string.Empty,
            LastSession = continuation.LastSession,
            NextStep = continuation.NextTask,
            Why = IntentExplainer.Explain(intent, situation),
            ShowContinue = !string.IsNullOrWhiteSpace(project) && intent.Kind is
                DetectedIntentKind.ContinueProject or DetectedIntentKind.ResumePreviousSession
                or DetectedIntentKind.PrepareWorkspace
        };
    }
}
