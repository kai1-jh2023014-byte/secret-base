namespace SecretBase.Core.Assistant;

public enum AiMessageRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3
}

public sealed class AiToolCall
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>JSON object payload from the model. Never a command line.</summary>
    public string ArgumentsJson { get; init; } = "{}";
}

public sealed class AiMessage
{
    public required AiMessageRole Role { get; init; }

    public string? Content { get; init; }

    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = Array.Empty<AiToolCall>();

    public string? ToolCallId { get; init; }
}

public enum AiProviderStatus
{
    Ok = 0,
    NotConfigured = 1,
    Unavailable = 2,
    Failed = 3
}

public sealed class AiProviderResponse
{
    public AiProviderStatus Status { get; init; } = AiProviderStatus.Ok;

    public string? Content { get; init; }

    public IReadOnlyList<AiToolCall> ToolCalls { get; init; } = Array.Empty<AiToolCall>();

    public string? ErrorMessage { get; init; }

    public static AiProviderResponse Text(string content) =>
        new() { Status = AiProviderStatus.Ok, Content = content };

    public static AiProviderResponse Tools(IReadOnlyList<AiToolCall> calls, string? content = null) =>
        new() { Status = AiProviderStatus.Ok, Content = content, ToolCalls = calls };

    public static AiProviderResponse NotConfigured() =>
        new()
        {
            Status = AiProviderStatus.NotConfigured,
            ErrorMessage = AssistantUserMessages.NotConfigured
        };

    public static AiProviderResponse Unavailable(string? detail = null) =>
        new()
        {
            Status = AiProviderStatus.Unavailable,
            ErrorMessage = string.IsNullOrWhiteSpace(detail)
                ? AssistantUserMessages.Unavailable
                : detail
        };

    public static AiProviderResponse Fail(string? detail = null) =>
        new()
        {
            Status = AiProviderStatus.Failed,
            ErrorMessage = string.IsNullOrWhiteSpace(detail)
                ? AssistantUserMessages.Unavailable
                : detail
        };
}

/// <summary>
/// LLM boundary. Implementations must not start processes or touch the filesystem.
/// Provider SDKs stay out of Core.
/// </summary>
public interface IAiProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default);
}
