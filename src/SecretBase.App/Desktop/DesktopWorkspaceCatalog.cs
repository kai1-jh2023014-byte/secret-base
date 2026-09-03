using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Workspace;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App.Desktop;

/// <summary>
/// Block items on the current desktop layout. Restore uses intake — never File.Delete of user files.
/// </summary>
public sealed class DesktopWorkspaceCatalog : IWorkspaceCatalog
{
    private readonly Func<DesktopLayout?> _layout;
    private readonly IBlockItemIntakeService _intake;
    private readonly Action _persist;
    private readonly Action<string>? _onStatus;

    public DesktopWorkspaceCatalog(
        Func<DesktopLayout?> layout,
        IBlockItemIntakeService intake,
        Action persist,
        Action<string>? onStatus = null)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _intake = intake ?? throw new ArgumentNullException(nameof(intake));
        _persist = persist ?? throw new ArgumentNullException(nameof(persist));
        _onStatus = onStatus;
    }

    public IReadOnlyList<WorkspaceNamedEntry> ListBlockItems()
    {
        var layout = _layout();
        if (layout is null)
        {
            return [];
        }

        return layout.Blocks
            .SelectMany(block => block.Items.Select(item => new WorkspaceNamedEntry
            {
                Name = item.Name,
                Kind = WorkspaceEntryKind.BlockItem,
                BlockId = block.Id,
                ItemId = item.Id,
                LaunchTarget = item.Target,
                ItemType = item.Type,
                HiddenFromDesktop = item.HiddenFromDesktop
            }))
            .ToList();
    }

    public bool TryRemoveBlockItem(Guid blockId, Guid itemId, out string? error)
    {
        error = null;
        var layout = _layout();
        var block = layout?.Blocks.FirstOrDefault(b => b.Id == blockId);
        var item = block?.Items.FirstOrDefault(i => i.Id == itemId);
        if (block is null || item is null)
        {
            error = "Block item was not found.";
            return false;
        }

        if (item.HiddenFromDesktop)
        {
            if (!_intake.TryRestoreToDesktop(item.Target, item.DesktopOriginPath, out _, out var restoreError))
            {
                error = restoreError ?? "Could not return the item to Desktop.";
                _onStatus?.Invoke(error);
                return false;
            }
        }

        block.Items.Remove(item);
        _persist();
        return true;
    }
}
