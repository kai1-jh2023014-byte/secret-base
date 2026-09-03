namespace SecretBase.Core.Workspace;

public enum WorkspaceCommandKind
{
    OpenNamed = 0,
    RemoveNamed = 1
}

/// <summary>Validated workspace intent. Never a free-form path or shell command.</summary>
public sealed class WorkspaceCommand
{
    public WorkspaceCommandKind Kind { get; init; }

    public string? Name { get; init; }

    public static WorkspaceCommand OpenNamed(string name) =>
        new() { Kind = WorkspaceCommandKind.OpenNamed, Name = name };

    public static WorkspaceCommand RemoveNamed(string name) =>
        new() { Kind = WorkspaceCommandKind.RemoveNamed, Name = name };
}
