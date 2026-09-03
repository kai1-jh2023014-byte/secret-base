using SecretBase.Core.Automation;
using SecretBase.Core.Session;
using SecretBase.Core.Situation;
using SecretBase.Core.State;

namespace SecretBase.Core.Intent;

public enum DetectedIntentKind
{
    Unknown = 0,
    ContinueProject = 1,
    StartFocus = 2,
    PrepareWorkspace = 3,
    ReviewTasks = 4,
    OpenProject = 5,
    EndWork = 6,
    TakeBreak = 7,
    MusicListening = 8,
    ResumePreviousSession = 9,
    ReviewCalendar = 10,
    SearchInformation = 11,
    OrganizeFiles = 12,
    QuickCapture = 13
}

public sealed class DetectedIntent
{
    public DetectedIntentKind Kind { get; init; } = DetectedIntentKind.Unknown;

    public double Confidence { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectId { get; init; }

    public string Rationale { get; init; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; init; } = [];

    public DateTimeOffset At { get; init; }

    public bool IsActionable =>
        Kind is not DetectedIntentKind.Unknown
        && Confidence >= IntentEngine.ActionThreshold;
}

/// <summary>
/// Evidence-based intent from Situation + User State. Not an LLM classifier. Intent is never an Action.
/// </summary>
public static class IntentEngine
{
    public const double ActionThreshold = 0.62;
    public const double AutoPrepareThreshold = 0.75;

    public static DetectedIntent Detect(UserState state, string? utterance = null) =>
        Detect(state, situation: null, session: null, utterance);

    public static DetectedIntent Detect(
        UserState state,
        CurrentSituation? situation,
        WorkSession? session = null,
        string? utterance = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!string.IsNullOrWhiteSpace(utterance))
        {
            var fromText = FromUtterance(utterance, state);
            if (fromText.Kind != DetectedIntentKind.Unknown)
            {
                return fromText;
            }
        }

        var evidence = new List<string>();
        if (situation is not null)
        {
            evidence.AddRange(situation.Evidence.Select(item => item.Fact));
        }

        if (state.FocusRunning)
        {
            evidence.Add("Focus timer is running.");
            return Finish(DetectedIntentKind.StartFocus, 0.9, state, evidence, "Focus is already running.");
        }

        if (!string.IsNullOrWhiteSpace(state.MusicLine)
            && string.IsNullOrWhiteSpace(state.CurrentProjectName)
            && state.CurrentCalendarTitle is null)
        {
            evidence.Add("Music is playing with no work block.");
            return Finish(DetectedIntentKind.MusicListening, 0.55, state, evidence, "Music is playing and no work block is visible.");
        }

        if (LooksLikeBreak(state.CurrentCalendarTitle) || LooksLikeBreak(state.UpcomingCalendarTitle))
        {
            evidence.Add("Calendar looks like a break.");
            return Finish(DetectedIntentKind.TakeBreak, 0.7, state, evidence, "Calendar looks like a break.");
        }

        var workTitle = state.CurrentCalendarTitle ?? state.UpcomingCalendarTitle;
        var nearWork = state.CurrentCalendarTitle is not null
                       || (state.UntilUpcoming is { } until && until <= TimeAwareAdvisor.LeadTime
                           && TimeAwareAdvisor.LooksLikeWork(workTitle));
        if (nearWork)
        {
            evidence.Add("Calendar work block matches the current moment.");
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentProjectName))
        {
            evidence.Add("Same project is active recently.");
        }

        if (!string.IsNullOrWhiteSpace(state.CurrentWorkspaceTitle))
        {
            evidence.Add("Previous workspace matches.");
        }

        if (session is not null && !string.IsNullOrWhiteSpace(session.Summary))
        {
            evidence.Add("Previous session: " + session.Summary);
        }

        if (state.OpenTodoCount > 0 && !string.IsNullOrWhiteSpace(state.ActiveTodo))
        {
            evidence.Add("Unfinished todo exists: " + state.ActiveTodo);
        }

        if (nearWork || !string.IsNullOrWhiteSpace(state.CurrentProjectName) || session is not null)
        {
            var confidence = situation?.Confidence ?? state.Confidence;
            if (nearWork)
            {
                confidence = Math.Min(0.95, confidence + 0.15);
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentWorkspaceTitle))
            {
                confidence = Math.Min(0.97, confidence + 0.05);
            }

            if (session is not null && !nearWork && string.IsNullOrWhiteSpace(state.CurrentCalendarTitle))
            {
                return Finish(
                    DetectedIntentKind.ResumePreviousSession,
                    Math.Round(Math.Min(0.93, confidence + 0.04), 2),
                    state,
                    evidence,
                    "A previous session can be resumed.",
                    session.ProjectName ?? state.CurrentProjectName,
                    session.ProjectId ?? state.CurrentProjectId);
            }

            return Finish(
                DetectedIntentKind.ContinueProject,
                Math.Round(confidence, 2),
                state,
                evidence,
                nearWork ? "A work block is on the calendar." : "A recent project/workspace is present.");
        }

        if (state.OpenTodoCount > 0 && string.IsNullOrWhiteSpace(state.CurrentProjectName))
        {
            evidence.Add($"{state.OpenTodoCount} open task(s).");
            return Finish(DetectedIntentKind.ReviewTasks, 0.58, state, evidence, $"{state.OpenTodoCount} open task(s).");
        }

        evidence.Add("Not enough signal.");
        return Finish(DetectedIntentKind.Unknown, 0.2, state, evidence, "Not enough signal.");
    }

    public static DetectedIntent FromUtterance(string text, UserState state)
    {
        if (text.Contains("休憩", StringComparison.Ordinal)
            || text.Contains("break", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(DetectedIntentKind.TakeBreak, 0.8, state, ["User asked for a break."], "User asked for a break.");
        }

        if (text.Contains("ポモドーロ", StringComparison.Ordinal)
            || text.Contains("pomodoro", StringComparison.OrdinalIgnoreCase)
            || text.Contains("集中", StringComparison.Ordinal))
        {
            return Finish(DetectedIntentKind.StartFocus, 0.85, state, ["User asked to focus."], "User asked to focus.");
        }

        if (text.Contains("予定", StringComparison.Ordinal)
            || text.Contains("calendar", StringComparison.OrdinalIgnoreCase)
            || text.Contains("tomorrow", StringComparison.OrdinalIgnoreCase)
            || text.Contains("明日", StringComparison.Ordinal))
        {
            return Finish(DetectedIntentKind.ReviewCalendar, 0.82, state, ["User asked about the calendar."], "User asked about the calendar.");
        }

        if (text.Contains("探して", StringComparison.Ordinal)
            || text.Contains("search", StringComparison.OrdinalIgnoreCase)
            || text.Contains("where did i", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(DetectedIntentKind.SearchInformation, 0.8, state, ["User asked to search."], "User asked to search.");
        }

        if (text.Contains("整理", StringComparison.Ordinal)
            || text.Contains("cleanup", StringComparison.OrdinalIgnoreCase)
            || text.Contains("unused", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(DetectedIntentKind.OrganizeFiles, 0.78, state, ["User asked to review files."], "User asked to review files.");
        }

        if (text.Contains("キャプチャ", StringComparison.Ordinal)
            || text.Contains("capture", StringComparison.OrdinalIgnoreCase)
            || text.Contains("メモ", StringComparison.Ordinal))
        {
            return Finish(DetectedIntentKind.QuickCapture, 0.84, state, ["User asked to capture a note."], "User asked to capture a note.");
        }
        if (text.Contains("昨日", StringComparison.Ordinal)
            || text.Contains("前回", StringComparison.Ordinal)
            || text.Contains("yesterday", StringComparison.OrdinalIgnoreCase)
            || text.Contains("last session", StringComparison.OrdinalIgnoreCase))
        {
            return Finish(
                DetectedIntentKind.ResumePreviousSession,
                0.9,
                state,
                ["User asked to resume the previous session."],
                "User asked to resume the previous session.");
        }

        if (text.Contains("続け", StringComparison.Ordinal)
            || text.Contains("再開", StringComparison.Ordinal)
            || text.Contains("continue", StringComparison.OrdinalIgnoreCase)
            || text.Contains("resume", StringComparison.OrdinalIgnoreCase)
            || text.Contains("開発", StringComparison.Ordinal))
        {
            return Finish(
                DetectedIntentKind.ContinueProject,
                0.88,
                state,
                ["User asked to continue work."],
                "User asked to continue work.");
        }

        return Finish(DetectedIntentKind.Unknown, 0.2, state, ["Utterance unmatched."], "Utterance unmatched.");
    }

    private static DetectedIntent Finish(
        DetectedIntentKind kind,
        double confidence,
        UserState state,
        IReadOnlyList<string> evidence,
        string rationale,
        string? projectName = null,
        string? projectId = null) =>
        new()
        {
            Kind = kind,
            Confidence = confidence,
            ProjectName = projectName ?? state.CurrentProjectName,
            ProjectId = projectId ?? state.CurrentProjectId,
            Rationale = rationale,
            Evidence = evidence,
            At = state.Now
        };

    private static bool LooksLikeBreak(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && (title.Contains("break", StringComparison.OrdinalIgnoreCase)
            || title.Contains("lunch", StringComparison.OrdinalIgnoreCase)
            || title.Contains("休憩", StringComparison.Ordinal));
}
