namespace SecretBase.Core.Assistant;

public sealed class AssistantActivity
{
    public required string Text { get; init; }

    public string? Domain { get; init; }

    public AssistantActivityStatus Status { get; init; } = AssistantActivityStatus.Running;
}

/// <summary>One or more actions waiting for Cancel / Run. Never HostAction without confirmation.</summary>
public sealed class AssistantPendingConfirmation
{
    public required string Prompt { get; init; }

    public IReadOnlyList<AssistantPendingAction> Actions { get; init; } = Array.Empty<AssistantPendingAction>();

    public bool HasRiskyAction { get; init; }

    public string ToolCallId => Actions.Count > 0 ? Actions[0].ToolCallId : string.Empty;

    public string ToolName => Actions.Count > 0 ? Actions[0].ToolName : string.Empty;

    public string ArgumentsJson => Actions.Count > 0 ? Actions[0].ArgumentsJson : "{}";
}

public sealed class AssistantTurnResult
{
    public bool Succeeded { get; init; }

    public string? AssistantText { get; init; }

    public string? ErrorMessage { get; init; }

    public bool NeedsConfiguration { get; init; }

    public AssistantResponseKind ResponseKind { get; init; } = AssistantResponseKind.Answer;

    public AssistantIntentKind Intent { get; init; } = AssistantIntentKind.Question;

    public AssistantPlan? Plan { get; init; }

    public IReadOnlyList<AssistantActivity> Activities { get; init; } = Array.Empty<AssistantActivity>();

    public AssistantPendingConfirmation? PendingConfirmation { get; init; }

    public IReadOnlyList<AssistantActionResult> ActionResults { get; init; } = Array.Empty<AssistantActionResult>();

    public bool CanRetry { get; init; }

    public string? RetryUserText { get; init; }

    public bool ShowOpenSettingsAction { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? CursorFolderPath { get; init; }

    /// <summary>Host should ensure this widget type is visible (add if missing). Comma-separated allowed.</summary>
    public string? EnsureWidgetType { get; init; }

    /// <summary>Host may arrange desktop widgets after ensuring types.</summary>
    public bool ShouldArrangeDesktop { get; init; }

    /// <summary>Routing / provider call metrics for this turn (no secrets).</summary>
    public AssistantRouteTrace? RouteTrace { get; init; }

    public static AssistantTurnResult Ok(
        string? text,
        IReadOnlyList<AssistantActivity>? activities = null,
        AssistantResponseKind kind = AssistantResponseKind.Answer,
        AssistantIntentKind intent = AssistantIntentKind.Question,
        AssistantPlan? plan = null,
        IReadOnlyList<AssistantActionResult>? actionResults = null,
        bool canRetry = false,
        string? retryUserText = null,
        bool showOpenSettingsAction = false,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        bool shouldOpenCursorAtFolder = false,
        string? cursorFolderPath = null,
        string? ensureWidgetType = null,
        bool shouldArrangeDesktop = false,
        AssistantRouteTrace? routeTrace = null) =>
        new()
        {
            Succeeded = true,
            AssistantText = text,
            ResponseKind = shouldLaunch || shouldOpenCursorAtFolder || shouldArrangeDesktop
                            || !string.IsNullOrWhiteSpace(ensureWidgetType)
                ? AssistantResponseKind.Execute
                : kind,
            Intent = intent,
            Plan = plan,
            Activities = activities ?? Array.Empty<AssistantActivity>(),
            ActionResults = actionResults ?? Array.Empty<AssistantActionResult>(),
            CanRetry = canRetry,
            RetryUserText = retryUserText,
            ShowOpenSettingsAction = showOpenSettingsAction,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            CursorFolderPath = cursorFolderPath,
            EnsureWidgetType = ensureWidgetType,
            ShouldArrangeDesktop = shouldArrangeDesktop,
            RouteTrace = routeTrace
        };

    public static AssistantTurnResult Confirm(
        AssistantPendingConfirmation pending,
        IReadOnlyList<AssistantActivity>? activities = null,
        AssistantPlan? plan = null,
        AssistantIntentKind intent = AssistantIntentKind.ActionRequest,
        AssistantRouteTrace? routeTrace = null) =>
        new()
        {
            Succeeded = true,
            ResponseKind = AssistantResponseKind.RequestConfirmation,
            Intent = intent,
            Plan = plan,
            PendingConfirmation = pending,
            Activities = activities ?? Array.Empty<AssistantActivity>(),
            RouteTrace = routeTrace
        };

    public static AssistantTurnResult Fail(
        string error,
        bool needsConfiguration = false,
        AssistantIntentKind intent = AssistantIntentKind.Question,
        AssistantPlan? plan = null,
        bool canRetry = false,
        string? retryUserText = null,
        bool showOpenSettingsAction = false,
        AssistantRouteTrace? routeTrace = null) =>
        new()
        {
            Succeeded = false,
            ResponseKind = AssistantResponseKind.Error,
            ErrorMessage = error,
            NeedsConfiguration = needsConfiguration,
            Intent = intent,
            Plan = plan,
            CanRetry = canRetry,
            RetryUserText = retryUserText,
            ShowOpenSettingsAction = showOpenSettingsAction || needsConfiguration,
            RouteTrace = routeTrace
        };
}

public interface IAssistantService
{
    IReadOnlyList<AiMessage> VisibleHistory { get; }

    AssistantProviderStatusInfo? ProviderStatus { get; }

    /// <summary>True while one or more Host actions await explicit Run/Cancel.</summary>
    bool HasPendingConfirmation { get; }

    void ClearSession();

    Task<AssistantTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default);

    Task<AssistantTurnResult> ConfirmPendingAsync(CancellationToken cancellationToken = default);

    Task<AssistantTurnResult> ContinueAfterCancelAsync(CancellationToken cancellationToken = default);

    void CancelPending();
}
