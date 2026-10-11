namespace SecretBase.Core.Assistant;

/// <summary>
/// Non-secret routing metrics for one assistant turn (tests / status UI).
/// Never includes API keys, tokens, or file contents.
/// </summary>
public sealed class AssistantRouteTrace
{
    public string RouteId { get; init; } = "none";

    public LocalFastPathKind FastPathKind { get; init; } = LocalFastPathKind.None;

    public bool CompletedLocally { get; init; }

    public bool CalledJev { get; init; }

    public bool CalledConversationModel { get; init; }

    public string? ConversationProviderId { get; init; }

    public string? FallbackReason { get; init; }

    public string? ConfirmationReason { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public bool Succeeded { get; init; }
}
