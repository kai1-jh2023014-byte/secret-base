using SecretBase.Core.Activity;
using SecretBase.Core.Automation;
using SecretBase.Core.State;
using SecretBase.Core.Workspace;

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
    MusicListening = 8
}

public sealed class DetectedIntent
{
    public DetectedIntentKind Kind { get; init; } = DetectedIntentKind.Unknown;

    public double Confidence { get; init; }

    public string? ProjectName { get; init; }

    public string? ProjectId { get; init; }

    public string Rationale { get; init; } = string.Empty;

    public bool IsActionable =>
        Kind is not DetectedIntentKind.Unknown
        && Confidence >= IntentEngine.ActionThreshold;
}

/// <summary>
/// Heuristic intent from User State. Not an LLM classifier. Intent is never an Action.
/// </summary>
public static class IntentEngine
{
    public const double ActionThreshold = 0.62;
    public const double AutoPrepareThreshold = 0.75;

    public static DetectedIntent Detect(UserState state, string? utterance = null)
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

        if (state.FocusRunning)
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.StartFocus,
                Confidence = 0.9,
                ProjectName = state.CurrentProjectName,
                ProjectId = state.CurrentProjectId,
                Rationale = "Focus is already running."
            };
        }

        if (!string.IsNullOrWhiteSpace(state.MusicLine)
            && string.IsNullOrWhiteSpace(state.CurrentProjectName)
            && state.CurrentCalendarTitle is null)
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.MusicListening,
                Confidence = 0.55,
                Rationale = "Music is playing and no work block is visible."
            };
        }

        if (LooksLikeBreak(state.CurrentCalendarTitle) || LooksLikeBreak(state.UpcomingCalendarTitle))
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.TakeBreak,
                Confidence = 0.7,
                Rationale = "Calendar looks like a break."
            };
        }

        var workTitle = state.CurrentCalendarTitle ?? state.UpcomingCalendarTitle;
        var nearWork = state.CurrentCalendarTitle is not null
                       || (state.UntilUpcoming is { } until && until <= TimeAwareAdvisor.LeadTime
                           && TimeAwareAdvisor.LooksLikeWork(workTitle));
        if (nearWork || !string.IsNullOrWhiteSpace(state.CurrentProjectName))
        {
            var confidence = state.Confidence;
            if (nearWork)
            {
                confidence = Math.Min(0.95, confidence + 0.15);
            }

            if (!string.IsNullOrWhiteSpace(state.CurrentWorkspaceTitle))
            {
                confidence = Math.Min(0.97, confidence + 0.05);
            }

            return new DetectedIntent
            {
                Kind = DetectedIntentKind.ContinueProject,
                Confidence = Math.Round(confidence, 2),
                ProjectName = state.CurrentProjectName ?? workTitle,
                ProjectId = state.CurrentProjectId,
                Rationale = nearWork
                    ? "A work block is on the calendar."
                    : "A recent project/workspace is present."
            };
        }

        if (state.OpenTodoCount > 0 && string.IsNullOrWhiteSpace(state.CurrentProjectName))
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.ReviewTasks,
                Confidence = 0.58,
                Rationale = $"{state.OpenTodoCount} open task(s)."
            };
        }

        return new DetectedIntent
        {
            Kind = DetectedIntentKind.Unknown,
            Confidence = 0.2,
            Rationale = "Not enough signal."
        };
    }

    public static DetectedIntent FromUtterance(string text, UserState state)
    {
        if (text.Contains("休憩", StringComparison.Ordinal)
            || text.Contains("break", StringComparison.OrdinalIgnoreCase))
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.TakeBreak,
                Confidence = 0.8,
                Rationale = "User asked for a break."
            };
        }

        if (text.Contains("ポモドーロ", StringComparison.Ordinal)
            || text.Contains("pomodoro", StringComparison.OrdinalIgnoreCase)
            || text.Contains("集中", StringComparison.Ordinal))
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.StartFocus,
                Confidence = 0.85,
                ProjectName = state.CurrentProjectName,
                ProjectId = state.CurrentProjectId,
                Rationale = "User asked to focus."
            };
        }

        if (text.Contains("続け", StringComparison.Ordinal)
            || text.Contains("再開", StringComparison.Ordinal)
            || text.Contains("continue", StringComparison.OrdinalIgnoreCase)
            || text.Contains("resume", StringComparison.OrdinalIgnoreCase)
            || text.Contains("開発", StringComparison.Ordinal))
        {
            return new DetectedIntent
            {
                Kind = DetectedIntentKind.ContinueProject,
                Confidence = 0.88,
                ProjectName = state.CurrentProjectName,
                ProjectId = state.CurrentProjectId,
                Rationale = "User asked to continue work."
            };
        }

        return new DetectedIntent { Kind = DetectedIntentKind.Unknown, Confidence = 0.2, Rationale = "Utterance unmatched." };
    }

    private static bool LooksLikeBreak(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && (title.Contains("break", StringComparison.OrdinalIgnoreCase)
            || title.Contains("lunch", StringComparison.OrdinalIgnoreCase)
            || title.Contains("休憩", StringComparison.Ordinal));
}
