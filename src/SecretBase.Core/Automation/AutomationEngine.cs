using SecretBase.Core.Activity;
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
    SuggestionFeedback = 6
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
    public static bool ShouldStayQuiet(
        UserState state,
        DetectedIntent intent,
        double urgency = 0.3)
    {
        if (state.FocusRunning && urgency < 0.7)
        {
            return true;
        }

        if (intent.Kind == DetectedIntentKind.Unknown || intent.Confidence < IntentEngine.ActionThreshold)
        {
            return true;
        }

        var hour = state.Now.Hour;
        var quietHours = hour >= 22 || hour < 8;
        if (quietHours && urgency < 0.8 && state.CurrentCalendarTitle is null)
        {
            return true;
        }

        return false;
    }
}

/// <summary>
/// Trigger → Intent → Plan → Safety → Suggestion. Actions still go through Confirmation.
/// </summary>
public static class AutomationEngine
{
    public static AutomationSuggestion? Evaluate(
        UserState state,
        DetectedIntent intent,
        IAutomationFeedbackStore? feedback = null,
        AutomationTriggerKind trigger = AutomationTriggerKind.Time)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(intent);

        var urgency = trigger is AutomationTriggerKind.Calendar or AutomationTriggerKind.Startup
            ? 0.65
            : 0.35;
        if (state.CurrentCalendarTitle is not null)
        {
            urgency = 0.7;
        }

        if (InterventionPolicy.ShouldStayQuiet(state, intent, urgency))
        {
            return null;
        }

        var rate = feedback?.AcceptanceRate(intent.Kind) ?? 0.5;
        var confidence = Math.Clamp(intent.Confidence * (0.7 + (rate * 0.3)), 0, 0.99);
        if (confidence < IntentEngine.ActionThreshold)
        {
            return null;
        }

        return intent.Kind switch
        {
            DetectedIntentKind.ContinueProject or DetectedIntentKind.PrepareWorkspace or DetectedIntentKind.OpenProject
                => new AutomationSuggestion
                {
                    Intent = DetectedIntentKind.ContinueProject,
                    Title = string.IsNullOrWhiteSpace(intent.ProjectName)
                        ? "Your workspace is ready."
                        : $"{intent.ProjectName} workspace is ready.",
                    Detail = "Continue development?",
                    Confidence = Math.Round(confidence, 2),
                    RequiresConfirmation = true,
                    Safety = AutomationSafetyLevel.SafeAuto,
                    ProjectName = intent.ProjectName
                },
            DetectedIntentKind.ReviewTasks => new AutomationSuggestion
            {
                Intent = DetectedIntentKind.ReviewTasks,
                Title = "Open tasks are waiting.",
                Detail = state.ActiveTodo ?? "Review your list.",
                Confidence = Math.Round(confidence, 2),
                RequiresConfirmation = false,
                Safety = AutomationSafetyLevel.SafeAuto
            },
            DetectedIntentKind.StartFocus => null,
            DetectedIntentKind.TakeBreak => new AutomationSuggestion
            {
                Intent = DetectedIntentKind.TakeBreak,
                Title = "A break is on the calendar.",
                Detail = "Focus stays idle.",
                Confidence = Math.Round(confidence, 2),
                RequiresConfirmation = false,
                Safety = AutomationSafetyLevel.SafeAuto
            },
            _ => null
        };
    }
}
