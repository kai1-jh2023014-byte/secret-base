using SecretBase.Core.Intent;
using SecretBase.Core.State;

namespace SecretBase.Core.Automation;

public enum AutomationTriggerKind
{
    Time = 0,
    Calendar = 1,
    Startup = 2,
    FocusEnded = 3,
    WorkspaceChanged = 4,
    UserActivity = 5,
    SuggestionFeedback = 6,
    Application = 7,
    SystemResume = 8,
    ProjectState = 9,
    FileState = 10,
    IntegrationEvent = 11
}

public enum InterventionMode
{
    Silent = 0,
    Passive = 1,
    Suggest = 2,
    Confirm = 3,
    Urgent = 4
}

public sealed class AutomationSuggestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public DetectedIntentKind Intent { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public bool RequiresConfirmation { get; init; } = true;

    public AutomationSafetyLevel Safety { get; init; } = AutomationSafetyLevel.SafeAuto;

    public string? ProjectName { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];
}

public sealed class AutomationFeedback
{
    public string SuggestionId { get; set; } = string.Empty;

    public DetectedIntentKind Intent { get; set; }

    public bool Accepted { get; set; }

    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;

    public string? ProjectName { get; set; }
}

public sealed class AutomationFeedbackDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<AutomationFeedback> Items { get; set; } = [];
}

public interface IAutomationFeedbackStore
{
    void Record(AutomationFeedback feedback);

    IReadOnlyList<AutomationFeedback> Recent(int take = 40);

    double AcceptanceRate(DetectedIntentKind intent);

    int ConsecutiveDismissals(DetectedIntentKind intent);
}

public sealed class AutomationFeedbackStore : IAutomationFeedbackStore
{
    private readonly object _gate = new();
    private readonly List<AutomationFeedback> _items = [];

    public void Record(AutomationFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);
        lock (_gate)
        {
            _items.Add(feedback);
            if (_items.Count > 200)
            {
                _items.RemoveRange(0, _items.Count - 200);
            }
        }
    }

    public IReadOnlyList<AutomationFeedback> Recent(int take = 40)
    {
        lock (_gate)
        {
            return _items.TakeLast(take).ToList();
        }
    }

    public double AcceptanceRate(DetectedIntentKind intent)
    {
        lock (_gate)
        {
            var slice = _items.Where(item => item.Intent == intent).TakeLast(20).ToList();
            if (slice.Count == 0)
            {
                return 0.5;
            }

            return slice.Count(item => item.Accepted) / (double)slice.Count;
        }
    }

    public int ConsecutiveDismissals(DetectedIntentKind intent)
    {
        lock (_gate)
        {
            return _items
                .Where(item => item.Intent == intent)
                .Reverse()
                .TakeWhile(item => !item.Accepted)
                .Count();
        }
    }

    public AutomationFeedbackDocument Snapshot()
    {
        lock (_gate)
        {
            return new AutomationFeedbackDocument { Items = _items.ToList() };
        }
    }

    public void ReplaceAll(IEnumerable<AutomationFeedback> items)
    {
        lock (_gate)
        {
            _items.Clear();
            _items.AddRange(items);
        }
    }
}

/// <summary>Quiet-by-default gate. Never launches OS. Focus and low urgency stay silent.</summary>
public static class InterventionPolicy
{
    public static readonly TimeSpan MinRepeat = TimeSpan.FromMinutes(10);

    public static bool ShouldStayQuiet(
        UserState state,
        DetectedIntent intent,
        double urgency = 0.3) =>
        Decide(state, intent, urgency, dismissals: 0, lastIntervention: null) is InterventionMode.Silent
            or InterventionMode.Passive;

    public static InterventionMode Decide(
        UserState state,
        DetectedIntent intent,
        double urgency = 0.3,
        int dismissals = 0,
        DateTimeOffset? lastIntervention = null,
        int quietHoursStart = 22,
        int quietHoursEnd = 8,
        bool allowFocusInterruptions = false)
    {
        if (state.FocusRunning && urgency < 0.7 && !allowFocusInterruptions)
        {
            return InterventionMode.Silent;
        }

        if (intent.Kind == DetectedIntentKind.Unknown || intent.Confidence < IntentEngine.ActionThreshold)
        {
            return InterventionMode.Silent;
        }

        var hour = state.Now.Hour;
        var quietHours = quietHoursStart > quietHoursEnd
            ? hour >= quietHoursStart || hour < quietHoursEnd
            : hour >= quietHoursStart && hour < quietHoursEnd;
        if (quietHours && urgency < 0.8 && state.CurrentCalendarTitle is null)
        {
            return InterventionMode.Silent;
        }

        if (lastIntervention is not null && state.Now - lastIntervention.Value < MinRepeat && urgency < 0.75)
        {
            return InterventionMode.Silent;
        }

        var mode = intent.Kind switch
        {
            DetectedIntentKind.ContinueProject
                or DetectedIntentKind.ResumePreviousSession
                or DetectedIntentKind.PrepareWorkspace
                or DetectedIntentKind.OpenProject => InterventionMode.Confirm,
            DetectedIntentKind.ReviewTasks or DetectedIntentKind.TakeBreak => InterventionMode.Suggest,
            _ => InterventionMode.Silent
        };
        return LearningPolicy.Cap(mode, dismissals);
    }
}

public static class LearningPolicy
{
    /// <summary>Learning never raises Safety / Confirmation to auto-action.</summary>
    public const bool MayEscalatePrivilege = false;

    public static InterventionMode Cap(InterventionMode proposed, int consecutiveDismissals)
    {
        if (consecutiveDismissals >= 3
            && proposed is InterventionMode.Suggest or InterventionMode.Confirm or InterventionMode.Urgent)
        {
            return InterventionMode.Passive;
        }

        return proposed;
    }
}

public static class SuggestionRanking
{
    public static double Adjust(double confidence, double acceptanceRate) =>
        Math.Clamp(confidence * (0.7 + (acceptanceRate * 0.3)), 0, 0.99);
}

public sealed class AutomationPlan
{
    public string Summary { get; init; } = string.Empty;

    public string PrepareStep { get; init; } = "Prepare workspace (Safe Auto).";

    public string ConfirmStep { get; init; } = "Open registered project after confirmation.";

    public bool RequiresConfirmation { get; init; } = true;
}

public sealed class AutomationExecution
{
    public InterventionMode Mode { get; init; } = InterventionMode.Silent;

    public AutomationSuggestion? Suggestion { get; init; }

    public AutomationPlan? Plan { get; init; }

    public string? Result { get; init; }

    public IReadOnlyList<string> Evidence { get; init; } = [];
}

/// <summary>
/// Trigger → Condition → Intent → Plan → Safety → Suggestion. Actions still go through Confirmation.
/// </summary>
public static class AutomationEngine
{
    public static AutomationSuggestion? Evaluate(
        UserState state,
        DetectedIntent intent,
        IAutomationFeedbackStore? feedback = null,
        AutomationTriggerKind trigger = AutomationTriggerKind.Time) =>
        Run(state, intent, feedback, trigger, lastIntervention: null).Suggestion;

    public static AutomationExecution Run(
        UserState state,
        DetectedIntent intent,
        IAutomationFeedbackStore? feedback = null,
        AutomationTriggerKind trigger = AutomationTriggerKind.Time,
        DateTimeOffset? lastIntervention = null,
        int quietHoursStart = 22,
        int quietHoursEnd = 8,
        bool allowFocusInterruptions = false)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);

        var urgency = trigger is AutomationTriggerKind.Calendar
            or AutomationTriggerKind.Startup
            or AutomationTriggerKind.SystemResume
            or AutomationTriggerKind.FocusEnded
            ? 0.65
            : 0.35;
        if (state.CurrentCalendarTitle is not null)
        {
            urgency = 0.7;
        }

        var dismissals = feedback?.ConsecutiveDismissals(intent.Kind) ?? 0;
        var mode = InterventionPolicy.Decide(
            state,
            intent,
            urgency,
            dismissals,
            lastIntervention,
            quietHoursStart,
            quietHoursEnd,
            allowFocusInterruptions);
        if (mode is InterventionMode.Silent or InterventionMode.Passive)
        {
            return new AutomationExecution
            {
                Mode = mode,
                Result = mode == InterventionMode.Silent ? "Stayed quiet." : "Noted without interrupting.",
                Evidence = intent.Evidence
            };
        }

        var rate = feedback?.AcceptanceRate(intent.Kind) ?? 0.5;
        var confidence = SuggestionRanking.Adjust(intent.Confidence, rate);
        if (confidence < IntentEngine.ActionThreshold)
        {
            return new AutomationExecution
            {
                Mode = InterventionMode.Silent,
                Result = "Confidence too low after ranking.",
                Evidence = intent.Evidence
            };
        }

        var suggestion = intent.Kind switch
        {
            DetectedIntentKind.ContinueProject
                or DetectedIntentKind.ResumePreviousSession
                or DetectedIntentKind.PrepareWorkspace
                or DetectedIntentKind.OpenProject
                => new AutomationSuggestion
                {
                    Intent = intent.Kind == DetectedIntentKind.ResumePreviousSession
                        ? DetectedIntentKind.ResumePreviousSession
                        : DetectedIntentKind.ContinueProject,
                    Title = string.IsNullOrWhiteSpace(intent.ProjectName)
                        ? "Continue where you left off?"
                        : $"{intent.ProjectName} workspace is ready.",
                    Detail = "Continue development?",
                    Confidence = Math.Round(confidence, 2),
                    RequiresConfirmation = true,
                    Safety = AutomationSafetyLevel.SafeAuto,
                    ProjectName = intent.ProjectName,
                    Evidence = intent.Evidence
                },
            DetectedIntentKind.ReviewTasks => new AutomationSuggestion
            {
                Intent = DetectedIntentKind.ReviewTasks,
                Title = "Open tasks are waiting.",
                Detail = state.ActiveTodo ?? "Review your list.",
                Confidence = Math.Round(confidence, 2),
                RequiresConfirmation = false,
                Safety = AutomationSafetyLevel.SafeAuto,
                Evidence = intent.Evidence
            },
            DetectedIntentKind.TakeBreak => new AutomationSuggestion
            {
                Intent = DetectedIntentKind.TakeBreak,
                Title = "A break is on the calendar.",
                Detail = "Focus stays idle.",
                Confidence = Math.Round(confidence, 2),
                RequiresConfirmation = false,
                Safety = AutomationSafetyLevel.SafeAuto,
                Evidence = intent.Evidence
            },
            _ => null
        };

        if (suggestion is null)
        {
            return new AutomationExecution { Mode = InterventionMode.Silent, Evidence = intent.Evidence };
        }

        return new AutomationExecution
        {
            Mode = mode,
            Suggestion = suggestion,
            Plan = new AutomationPlan
            {
                Summary = suggestion.Title,
                RequiresConfirmation = suggestion.RequiresConfirmation
            },
            Result = "Suggestion prepared. Launch still requires confirmation.",
            Evidence = intent.Evidence
        };
    }
}
