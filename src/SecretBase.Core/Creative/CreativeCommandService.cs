namespace SecretBase.Core.Creative;

/// <summary>
/// Validates CreativeCommands against the registered workspace only.
/// Never accepts free-form paths from AI; never deletes/moves/runs shell.
/// Host performs actual open via <c>ITargetLaunchService</c> when <see cref="CreativeCommandResult.ShouldLaunch"/>.
/// </summary>
public sealed class CreativeCommandService
{
    public const int MaxQueryLength = 200;

    private readonly CreativeWorkspaceService _workspace;

    public CreativeCommandService(CreativeWorkspaceService workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public CreativeWorkspaceService Workspace => _workspace;

    public CreativeCommandResult Execute(CreativeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            CreativeCommandKind.SearchItems => Search(command),
            CreativeCommandKind.OpenItem => Open(command, requireType: null),
            CreativeCommandKind.OpenFolder => Open(command, requireType: CreativeItemType.Folder),
            CreativeCommandKind.OpenProject => Open(command, requireType: CreativeItemType.Project),
            CreativeCommandKind.ToggleFavorite => ToggleFavorite(command),
            _ => CreativeCommandResult.Fail(command.Kind, "Unknown creative command.")
        };
    }

    private CreativeCommandResult Search(CreativeCommand command)
    {
        var query = command.Query?.Trim() ?? string.Empty;
        if (query.Length > MaxQueryLength)
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.SearchItems, "Search query is too long.");
        }

        if (query.Contains("://", StringComparison.Ordinal)
            || query.Contains("..", StringComparison.Ordinal))
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.SearchItems, "Search query is not allowed.");
        }

        var items = _workspace.Search(query);
        return CreativeCommandResult.Ok(CreativeCommandKind.SearchItems, items);
    }

    private CreativeCommandResult Open(CreativeCommand command, CreativeItemType? requireType)
    {
        if (string.IsNullOrWhiteSpace(command.ItemId))
        {
            return CreativeCommandResult.Fail(command.Kind, "Item id is missing.");
        }

        var item = _workspace.FindById(command.ItemId);
        if (item is null)
        {
            return CreativeCommandResult.Fail(command.Kind, "Item is not registered in Creative Workspace.");
        }

        if (requireType is CreativeItemType.Folder
            && item.ItemType is not (CreativeItemType.Folder or CreativeItemType.Project))
        {
            return CreativeCommandResult.Fail(command.Kind, "Item is not a folder.");
        }

        if (requireType is CreativeItemType.Project && item.ItemType != CreativeItemType.Project)
        {
            return CreativeCommandResult.Fail(command.Kind, "Item is not a project.");
        }

        if (!_workspace.TryMarkOpened(item.Id, DateTimeOffset.UtcNow, out var updated, out var error))
        {
            return CreativeCommandResult.Fail(command.Kind, error ?? "Could not update Recent.");
        }

        return CreativeCommandResult.Ok(command.Kind, item: updated, shouldLaunch: true);
    }

    private CreativeCommandResult ToggleFavorite(CreativeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ItemId))
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.ToggleFavorite, "Item id is missing.");
        }

        if (!_workspace.TryToggleFavorite(command.ItemId!, out var item, out var error))
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.ToggleFavorite, error ?? "Toggle failed.");
        }

        return CreativeCommandResult.Ok(CreativeCommandKind.ToggleFavorite, item: item);
    }
}
