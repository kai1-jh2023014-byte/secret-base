using SecretBase.Core.Security;

namespace SecretBase.Core.Assistant;

public sealed class AssistantToolParameter
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Description { get; init; }

    public bool Required { get; init; }
}

/// <summary>
/// A function the LLM may call. Distinct from <c>SecretBase.Core.Ai.AiToolDefinition</c>
/// (that type is the Cursor/ChatGPT launcher catalog).
/// </summary>
public sealed class AssistantToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<AssistantToolParameter> Parameters { get; init; } = Array.Empty<AssistantToolParameter>();

    public ActionPrivilege RiskLevel { get; init; } = ActionPrivilege.Observation;

    public bool RequiresConfirmation { get; init; }
}

public interface IAiToolRegistry
{
    IReadOnlyList<AssistantToolDefinition> Tools { get; }

    AssistantToolDefinition? Find(string? name);
}

public sealed class AssistantToolResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>JSON/text returned to the LLM (no secrets).</summary>
    public string ContentForModel { get; init; } = string.Empty;

    /// <summary>Short activity line for the widget (not chain-of-thought).</summary>
    public string? Activity { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? CursorFolderPath { get; init; }

    public static AssistantToolResult Ok(
        string contentForModel,
        string? activity = null,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        bool shouldOpenCursorAtFolder = false,
        string? cursorFolderPath = null) =>
        new()
        {
            Succeeded = true,
            ContentForModel = contentForModel,
            Activity = activity,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            CursorFolderPath = cursorFolderPath
        };

    public static AssistantToolResult Fail(string error, string? activity = null) =>
        new()
        {
            Succeeded = false,
            ErrorMessage = error,
            ContentForModel = error,
            Activity = activity
        };
}

public interface IAiToolExecutor
{
    Task<AssistantToolResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}
