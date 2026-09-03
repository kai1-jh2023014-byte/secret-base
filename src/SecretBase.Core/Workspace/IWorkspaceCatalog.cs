namespace SecretBase.Core.Workspace;

/// <summary>
/// Host-owned Block items. Core never calls File.Delete; restore uses the intake adapter.
/// </summary>
public interface IWorkspaceCatalog
{
    IReadOnlyList<WorkspaceNamedEntry> ListBlockItems();

    /// <summary>
    /// Returns a hidden Block item to Desktop and removes it from the Block.
    /// Linked (not moved) items are only unregistered. Never deletes OS files.
    /// </summary>
    bool TryRemoveBlockItem(Guid blockId, Guid itemId, out string? error);
}
