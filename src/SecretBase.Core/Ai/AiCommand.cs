namespace SecretBase.Core.Ai;

/// <summary>Allowed AI Workspace operations for UI and future AI commands.</summary>
public enum AiCommandKind
{
    ListTools = 0,
    OpenTool = 1,
    OpenProjectInCursor = 2,
    OpenCursorWebsite = 3
}

/// <summary>
/// Validated AI hub intent. Future AI must emit these — never free-form Process/PowerShell.
/// </summary>
public sealed class AiCommand
{
    public AiCommandKind Kind { get; init; }

    public string? ToolId { get; init; }

    public string? ProjectId { get; init; }

    public static AiCommand ListTools() => new() { Kind = AiCommandKind.ListTools };

    public static AiCommand OpenTool(string toolId) =>
        new() { Kind = AiCommandKind.OpenTool, ToolId = toolId };

    public static AiCommand OpenProjectInCursor(string projectId) =>
        new() { Kind = AiCommandKind.OpenProjectInCursor, ProjectId = projectId };

    public static AiCommand OpenCursorWebsite() =>
        new() { Kind = AiCommandKind.OpenCursorWebsite, ToolId = AiBuiltinTools.CursorId };
}
