using SecretBase.Core.Ai;

namespace SecretBase.Core.Creative;

/// <summary>
/// Validates CreativeCommands against registered workspace/projects only.
/// Never accepts free-form paths from AI; never deletes/moves/runs shell.
/// Host performs actual open via <c>ITargetLaunchService</c> / browser / Cursor API when requested.
/// </summary>
public sealed class CreativeCommandService
{
    public const int MaxQueryLength = 200;

    private readonly CreativeWorkspaceService _workspace;
    private readonly CreativeProjectService? _projects;
    private readonly AiCommandService? _ai;

    public CreativeCommandService(
        CreativeWorkspaceService workspace,
        CreativeProjectService? projects = null,
        AiCommandService? ai = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _projects = projects;
        _ai = ai;
    }

    public CreativeWorkspaceService Workspace => _workspace;

    public CreativeProjectService? Projects => _projects;

    public AiCommandService? Ai => _ai;

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
            CreativeCommandKind.SearchProjects => SearchProjects(command),
            CreativeCommandKind.GetCreativeProject => GetCreativeProject(command),
            CreativeCommandKind.OpenCreativeProject => OpenCreativeProject(command, openRoot: false),
            CreativeCommandKind.OpenCreativeProjectRoot => OpenCreativeProject(command, openRoot: true),
            CreativeCommandKind.OpenCreativeProjectResource => OpenCreativeProjectResource(command),
            CreativeCommandKind.ToggleCreativeProjectFavorite => ToggleCreativeProjectFavorite(command),
            CreativeCommandKind.DeleteCreativeProjectRegistration => DeleteCreativeProjectRegistration(command),
            CreativeCommandKind.SaveCreativeProjectNotes => SaveCreativeProjectNotes(command),
            CreativeCommandKind.ToggleCreativeProjectResourceQuickAction => ToggleResourceQuickAction(command),
            CreativeCommandKind.OpenProjectInCursor => MapAi(AiCommand.OpenProjectInCursor(command.ProjectId ?? string.Empty)),
            CreativeCommandKind.OpenAiTool => MapAi(AiCommand.OpenTool(command.ItemId ?? string.Empty)),
            _ => CreativeCommandResult.Fail(command.Kind, "Unknown creative command.")
        };
    }

    private CreativeCommandResult MapAi(AiCommand aiCommand)
    {
        if (_ai is null)
        {
            return CreativeCommandResult.Fail(aiCommand.Kind == AiCommandKind.OpenProjectInCursor
                ? CreativeCommandKind.OpenProjectInCursor
                : CreativeCommandKind.OpenAiTool, "AI commands are not available.");
        }

        var ai = _ai.Execute(aiCommand);
        var kind = aiCommand.Kind == AiCommandKind.OpenProjectInCursor
            ? CreativeCommandKind.OpenProjectInCursor
            : CreativeCommandKind.OpenAiTool;

        if (!ai.Succeeded)
        {
            return new CreativeCommandResult
            {
                Succeeded = false,
                Kind = kind,
                ErrorMessage = ai.ErrorMessage,
                OfferCursorWebsiteFallback = ai.OfferCursorWebsiteFallback,
                LaunchTarget = ai.Url,
                LaunchIsExternalLink = ai.OfferCursorWebsiteFallback && !string.IsNullOrWhiteSpace(ai.Url)
            };
        }

        return CreativeCommandResult.Ok(
            kind,
            shouldLaunch: ai.ShouldOpenUrl,
            launchTarget: ai.Url,
            launchIsExternalLink: ai.ShouldOpenUrl,
            shouldOpenCursorAtFolder: ai.ShouldOpenCursorAtFolder,
            cursorFolderPath: ai.FolderPath,
            shouldOpenCursorApp: ai.ShouldOpenCursorApp,
            offerCursorWebsiteFallback: ai.OfferCursorWebsiteFallback);
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

        return CreativeCommandResult.Ok(
            command.Kind,
            item: updated,
            shouldLaunch: true,
            launchTarget: updated!.Path);
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

    private CreativeCommandResult RequireProjects(CreativeCommandKind kind, out CreativeProjectService projects)
    {
        projects = _projects!;
        if (_projects is null)
        {
            return CreativeCommandResult.Fail(kind, "Projects are not available.");
        }

        return CreativeCommandResult.Ok(kind);
    }

    private CreativeCommandResult SearchProjects(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.SearchProjects, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        var query = command.Query?.Trim() ?? string.Empty;
        if (query.Length > MaxQueryLength)
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.SearchProjects, "Search query is too long.");
        }

        if (query.Contains("://", StringComparison.Ordinal)
            || query.Contains("..", StringComparison.Ordinal))
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.SearchProjects, "Search query is not allowed.");
        }

        return CreativeCommandResult.Ok(
            CreativeCommandKind.SearchProjects,
            projects: projects.Search(query));
    }

    private CreativeCommandResult GetCreativeProject(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.GetCreativeProject, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.GetCreativeProject, "Project id is missing.");
        }

        var project = projects.FindById(command.ProjectId);
        if (project is null)
        {
            return CreativeCommandResult.Fail(CreativeCommandKind.GetCreativeProject, "Project is not registered.");
        }

        return CreativeCommandResult.Ok(CreativeCommandKind.GetCreativeProject, project: project);
    }

    private CreativeCommandResult OpenCreativeProject(CreativeCommand command, bool openRoot)
    {
        var kind = openRoot
            ? CreativeCommandKind.OpenCreativeProjectRoot
            : CreativeCommandKind.OpenCreativeProject;
        var gate = RequireProjects(kind, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CreativeCommandResult.Fail(kind, "Project id is missing.");
        }

        var project = projects.FindById(command.ProjectId);
        if (project is null)
        {
            return CreativeCommandResult.Fail(kind, "Project is not registered.");
        }

        // OpenCreativeProject without forcing root = Dashboard entry (no auto-launch).
        if (!openRoot)
        {
            if (!projects.TryMarkOpened(project.Id, DateTimeOffset.UtcNow, out var marked, out var markError))
            {
                return CreativeCommandResult.Fail(kind, markError ?? "Could not update project.");
            }

            return CreativeCommandResult.Ok(kind, project: marked, shouldLaunch: false);
        }

        if (string.IsNullOrWhiteSpace(project.RootFolder))
        {
            return CreativeCommandResult.Fail(kind, "Project has no root folder.");
        }

        if (!projects.TryRecordRootOpened(project.Id, DateTimeOffset.UtcNow, out var updated, out var error))
        {
            return CreativeCommandResult.Fail(kind, error ?? "Could not update project.");
        }

        return CreativeCommandResult.Ok(
            kind,
            project: updated,
            shouldLaunch: true,
            launchTarget: updated!.RootFolder);
    }

    private CreativeCommandResult OpenCreativeProjectResource(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.OpenCreativeProjectResource, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId) || string.IsNullOrWhiteSpace(command.ResourceId))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.OpenCreativeProjectResource,
                "Project or resource id is missing.");
        }

        if (!projects.TryRecordResourceOpened(
                command.ProjectId!,
                command.ResourceId!,
                DateTimeOffset.UtcNow,
                out var updated,
                out var resource,
                out var error))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.OpenCreativeProjectResource,
                error ?? "Resource is not registered.");
        }

        var isLink = resource!.Kind == CreativeProjectResourceKind.ExternalLink;
        return CreativeCommandResult.Ok(
            CreativeCommandKind.OpenCreativeProjectResource,
            project: updated,
            resource: resource,
            shouldLaunch: true,
            launchTarget: resource.Target,
            launchIsExternalLink: isLink);
    }

    private CreativeCommandResult ToggleCreativeProjectFavorite(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.ToggleCreativeProjectFavorite, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.ToggleCreativeProjectFavorite,
                "Project id is missing.");
        }

        if (!projects.TryToggleFavorite(command.ProjectId!, out var project, out var error))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.ToggleCreativeProjectFavorite,
                error ?? "Toggle failed.");
        }

        return CreativeCommandResult.Ok(
            CreativeCommandKind.ToggleCreativeProjectFavorite,
            project: project);
    }

    private CreativeCommandResult DeleteCreativeProjectRegistration(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.DeleteCreativeProjectRegistration, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.DeleteCreativeProjectRegistration,
                "Project id is missing.");
        }

        if (!projects.TryDeleteRegistration(command.ProjectId!, out var error))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.DeleteCreativeProjectRegistration,
                error ?? "Delete registration failed.");
        }

        return CreativeCommandResult.Ok(CreativeCommandKind.DeleteCreativeProjectRegistration);
    }

    private CreativeCommandResult SaveCreativeProjectNotes(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.SaveCreativeProjectNotes, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.SaveCreativeProjectNotes,
                "Project id is missing.");
        }

        if (!projects.TrySaveNotes(command.ProjectId!, command.Notes, out var project, out var error))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.SaveCreativeProjectNotes,
                error ?? "Could not save notes.");
        }

        return CreativeCommandResult.Ok(CreativeCommandKind.SaveCreativeProjectNotes, project: project);
    }

    private CreativeCommandResult ToggleResourceQuickAction(CreativeCommand command)
    {
        var gate = RequireProjects(CreativeCommandKind.ToggleCreativeProjectResourceQuickAction, out var projects);
        if (!gate.Succeeded)
        {
            return gate;
        }

        if (string.IsNullOrWhiteSpace(command.ProjectId) || string.IsNullOrWhiteSpace(command.ResourceId))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.ToggleCreativeProjectResourceQuickAction,
                "Project or resource id is missing.");
        }

        if (!projects.TryToggleResourceQuickAction(
                command.ProjectId!,
                command.ResourceId!,
                out var project,
                out var error))
        {
            return CreativeCommandResult.Fail(
                CreativeCommandKind.ToggleCreativeProjectResourceQuickAction,
                error ?? "Toggle failed.");
        }

        return CreativeCommandResult.Ok(
            CreativeCommandKind.ToggleCreativeProjectResourceQuickAction,
            project: project);
    }
}
