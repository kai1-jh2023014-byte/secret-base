namespace SecretBase.Core.Assistant;

public sealed class AssistantActivity
{
    public required string Text { get; init; }

    public string? Domain { get; init; }

    public AssistantActivityStatus Status { get; init; } = AssistantActivityStatus.Running;
}

public sealed class AssistantPendingConfirmation
{
    public required string ToolCallId { get; init; }

    public required string ToolName { get; init; }

    public required string ArgumentsJson { get; init; }

    public required string Prompt { get; init; }
}

public sealed class AssistantTurnResult
{
    public bool Succeeded { get; init; }

    public string? AssistantText { get; init; }

    public string? ErrorMessage { get; init; }

    public bool NeedsConfiguration { get; init; }

    public AssistantResponseKind ResponseKind { get; init; } = AssistantResponseKind.Answer;

    public IReadOnlyList<AssistantActivity> Activities { get; init; } = Array.Empty<AssistantActivity>();

    public AssistantPendingConfirmation? PendingConfirmation { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? CursorFolderPath { get; init; }

    public static AssistantTurnResult Ok(
        string? text,
        IReadOnlyList<AssistantActivity>? activities = null,
        AssistantResponseKind kind = AssistantResponseKind.Answer,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        bool shouldOpenCursorAtFolder = false,
        string? cursorFolderPath = null) =>
        new()
        {
            Succeeded = true,
            AssistantText = text,
            ResponseKind = shouldLaunch || shouldOpenCursorAtFolder
                ? AssistantResponseKind.Execute
                : kind,
            Activities = activities ?? Array.Empty<AssistantActivity>(),
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            CursorFolderPath = cursorFolderPath
        };

    public static AssistantTurnResult Confirm(
        AssistantPendingConfirmation pending,
        IReadOnlyList<AssistantActivity>? activities = null) =>
        new()
        {
            Succeeded = true,
            ResponseKind = AssistantResponseKind.RequestConfirmation,
            PendingConfirmation = pending,
            Activities = activities ?? Array.Empty<AssistantActivity>()
        };

    public static AssistantTurnResult Fail(string error, bool needsConfiguration = false) =>
        new()
        {
            Succeeded = false,
            ResponseKind = AssistantResponseKind.Error,
            ErrorMessage = error,
            NeedsConfiguration = needsConfiguration
        };
}

public interface IAssistantService
{
    IReadOnlyList<AiMessage> VisibleHistory { get; }

    AssistantProviderStatusInfo? ProviderStatus { get; }

    void ClearSession();

    Task<AssistantTurnResult> SendAsync(string userText, CancellationToken cancellationToken = default);

    Task<AssistantTurnResult> ConfirmPendingAsync(CancellationToken cancellationToken = default);

    Task<AssistantTurnResult> ContinueAfterCancelAsync(CancellationToken cancellationToken = default);

    void CancelPending();
}
