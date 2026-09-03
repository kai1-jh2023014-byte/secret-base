using SecretBase.Core.Blocks;

namespace SecretBase.Core.Workspace;

public enum WorkspaceEntryKind
{
    App = 0,
    Project = 1,
    BlockItem = 2,
    LocalEvent = 3
}

/// <summary>A named thing the user can open or unregister inside Secret Base.</summary>
public sealed class WorkspaceNamedEntry
{
    public required string Name { get; init; }

    public required WorkspaceEntryKind Kind { get; init; }

    public string? AppId { get; init; }

    public string? ProjectId { get; init; }

    public Guid? BlockId { get; init; }

    public Guid? ItemId { get; init; }

    public string? EventId { get; init; }

    public string? LaunchTarget { get; init; }

    public BlockItemType ItemType { get; init; }

    public bool HiddenFromDesktop { get; init; }

    public bool LaunchIsExternalLink { get; init; }
}
