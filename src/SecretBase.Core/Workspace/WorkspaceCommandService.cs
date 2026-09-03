using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Workspace;

/// <summary>
/// Opens or unregisters named Secret Base items. Never deletes files on disk.
/// </summary>
public sealed class WorkspaceCommandService
{
    public const string DiskDeleteRefused =
        "Secret Base will not delete files on disk. Name a Block item (returns to Desktop), My App, Creative Project, or local calendar event to remove its Secret Base registration.";

    public const string NamedItemNotFound =
        "That name is not registered in Secret Base. Use a Block item, My App, Creative Project, or local event name.";

    private readonly AppCommandService? _apps;
    private readonly CreativeCommandService? _creative;
    private readonly CalendarCommandService? _calendar;
    private readonly IWorkspaceCatalog? _catalog;

    public WorkspaceCommandService(
        AppCommandService? apps = null,
        CreativeCommandService? creative = null,
        CalendarCommandService? calendar = null,
        IWorkspaceCatalog? catalog = null)
    {
        _apps = apps;
        _creative = creative;
        _calendar = calendar;
        _catalog = catalog;
    }

    public WorkspaceCommandResult Execute(WorkspaceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var name = command.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return WorkspaceCommandResult.Fail(command.Kind, "Name is required.");
        }

        return command.Kind switch
        {
            WorkspaceCommandKind.OpenNamed => OpenNamed(name),
            WorkspaceCommandKind.RemoveNamed => RemoveNamed(name),
            _ => WorkspaceCommandResult.Fail(command.Kind, "Unknown workspace command.")
        };
    }

    private WorkspaceCommandResult OpenNamed(string name)
    {
        var entries = Collect();
        var match = BestMatch(entries, name);
        if (match is null)
        {
            return WorkspaceCommandResult.Fail(WorkspaceCommandKind.OpenNamed, NamedItemNotFound);
        }

        return match.Kind switch
        {
            WorkspaceEntryKind.App when _apps is not null && !string.IsNullOrWhiteSpace(match.AppId) =>
                FromApp(_apps.Execute(AppCommand.OpenApp(match.AppId)), match),
            WorkspaceEntryKind.Project when _creative is not null && !string.IsNullOrWhiteSpace(match.ProjectId) =>
                FromCreative(_creative.Execute(CreativeCommand.OpenCreativeProject(match.ProjectId)), match),
            WorkspaceEntryKind.BlockItem when !string.IsNullOrWhiteSpace(match.LaunchTarget) =>
                WorkspaceCommandResult.Ok(
                    WorkspaceCommandKind.OpenNamed,
                    $"Host may open {match.Name}.",
                    match,
                    shouldLaunch: true,
                    launchTarget: match.LaunchTarget,
                    launchIsExternalLink: match.LaunchIsExternalLink),
            WorkspaceEntryKind.LocalEvent =>
                WorkspaceCommandResult.Ok(
                    WorkspaceCommandKind.OpenNamed,
                    $"'{match.Name}' is a local calendar event. It is already on today's agenda in the Calendar widget.",
                    match),
            _ => WorkspaceCommandResult.Fail(WorkspaceCommandKind.OpenNamed, NamedItemNotFound)
        };
    }

    private WorkspaceCommandResult RemoveNamed(string name)
    {
        var entries = Collect();
        var match = BestMatch(entries, name);
        if (match is null)
        {
            return WorkspaceCommandResult.Fail(WorkspaceCommandKind.RemoveNamed, DiskDeleteRefused);
        }

        switch (match.Kind)
        {
            case WorkspaceEntryKind.BlockItem when _catalog is not null
                                                    && match.BlockId is { } blockId
                                                    && match.ItemId is { } itemId:
            {
                if (!_catalog.TryRemoveBlockItem(blockId, itemId, out var error))
                {
                    return WorkspaceCommandResult.Fail(
                        WorkspaceCommandKind.RemoveNamed,
                        error ?? "Could not return the Block item to Desktop. It was left in the Block.");
                }

                var msg = match.HiddenFromDesktop
                    ? $"Returned '{match.Name}' to the Desktop and removed it from the Block."
                    : $"Removed '{match.Name}' from the Block. The original file was not deleted.";
                return WorkspaceCommandResult.Ok(WorkspaceCommandKind.RemoveNamed, msg, match);
            }
            case WorkspaceEntryKind.App when _apps is not null && !string.IsNullOrWhiteSpace(match.AppId):
            {
                var result = _apps.Execute(AppCommand.RemoveApp(match.AppId));
                return result.Succeeded
                    ? WorkspaceCommandResult.Ok(
                        WorkspaceCommandKind.RemoveNamed,
                        $"Unregistered My App '{match.Name}'. The application on disk was not deleted.",
                        match)
                    : WorkspaceCommandResult.Fail(
                        WorkspaceCommandKind.RemoveNamed,
                        result.ErrorMessage ?? "Could not unregister the app.");
            }
            case WorkspaceEntryKind.Project when _creative is not null && !string.IsNullOrWhiteSpace(match.ProjectId):
            {
                var result = _creative.Execute(CreativeCommand.DeleteCreativeProjectRegistration(match.ProjectId));
                return result.Succeeded
                    ? WorkspaceCommandResult.Ok(
                        WorkspaceCommandKind.RemoveNamed,
                        $"Unregistered Creative Project '{match.Name}'. Files on disk were not deleted.",
                        match)
                    : WorkspaceCommandResult.Fail(
                        WorkspaceCommandKind.RemoveNamed,
                        result.ErrorMessage ?? "Could not unregister the project.");
            }
            case WorkspaceEntryKind.LocalEvent when _calendar is not null && !string.IsNullOrWhiteSpace(match.EventId):
            {
                var removed = _calendar.ExecuteAsync(CalendarCommand.RemoveEvent(match.EventId))
                    .GetAwaiter()
                    .GetResult();
                return removed.Succeeded
                    ? WorkspaceCommandResult.Ok(
                        WorkspaceCommandKind.RemoveNamed,
                        $"Removed local calendar event '{match.Name}'.",
                        match)
                    : WorkspaceCommandResult.Fail(
                        WorkspaceCommandKind.RemoveNamed,
                        removed.ErrorMessage ?? "Could not remove the local event.");
            }
            default:
                return WorkspaceCommandResult.Fail(WorkspaceCommandKind.RemoveNamed, DiskDeleteRefused);
        }
    }

    private List<WorkspaceNamedEntry> Collect()
    {
        var list = new List<WorkspaceNamedEntry>();
        if (_apps is not null)
        {
            var apps = _apps.Execute(AppCommand.ListApps());
            if (apps.Succeeded)
            {
                foreach (var app in apps.Apps)
                {
                    list.Add(new WorkspaceNamedEntry
                    {
                        Name = app.Name,
                        Kind = WorkspaceEntryKind.App,
                        AppId = app.Id,
                        LaunchTarget = app.LaunchTarget,
                        LaunchIsExternalLink = app.Type == CustomAppType.Website
                    });
                }
            }
        }

        if (_catalog is not null)
        {
            list.AddRange(_catalog.ListBlockItems());
        }

        if (_creative is not null)
        {
            var projects = _creative.Execute(CreativeCommand.SearchProjects(null));
            if (projects.Succeeded)
            {
                foreach (var project in projects.Projects)
                {
                    list.Add(new WorkspaceNamedEntry
                    {
                        Name = project.Name,
                        Kind = WorkspaceEntryKind.Project,
                        ProjectId = project.Id
                    });
                }
            }
        }

        if (_calendar is not null)
        {
            foreach (var ev in _calendar.ListLocalEvents())
            {
                if (string.IsNullOrWhiteSpace(ev.Title))
                {
                    continue;
                }

                list.Add(new WorkspaceNamedEntry
                {
                    Name = ev.Title,
                    Kind = WorkspaceEntryKind.LocalEvent,
                    EventId = ev.Id
                });
            }
        }

        return list;
    }

    internal static WorkspaceNamedEntry? BestMatch(IReadOnlyList<WorkspaceNamedEntry> entries, string query)
    {
        var q = query.Trim();
        if (entries.Count == 0 || string.IsNullOrWhiteSpace(q))
        {
            return null;
        }

        var exact = entries.FirstOrDefault(e =>
            e.Name.Equals(q, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var contains = entries
            .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || q.Contains(e.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Name.Length)
            .ToList();
        return contains.Count == 0 ? null : contains[0];
    }

    private static WorkspaceCommandResult FromApp(AppCommandResult result, WorkspaceNamedEntry match) =>
        result.Succeeded
            ? WorkspaceCommandResult.Ok(
                WorkspaceCommandKind.OpenNamed,
                $"Host may launch {match.Name}.",
                match,
                shouldLaunch: result.ShouldLaunch,
                launchTarget: result.LaunchTarget,
                launchIsExternalLink: result.LaunchIsExternalLink)
            : WorkspaceCommandResult.Fail(WorkspaceCommandKind.OpenNamed, result.ErrorMessage ?? NamedItemNotFound);

    private static WorkspaceCommandResult FromCreative(CreativeCommandResult result, WorkspaceNamedEntry match) =>
        result.Succeeded
            ? WorkspaceCommandResult.Ok(
                WorkspaceCommandKind.OpenNamed,
                $"Opened Creative Project dashboard for {match.Name}.",
                match)
            : WorkspaceCommandResult.Fail(WorkspaceCommandKind.OpenNamed, result.ErrorMessage ?? NamedItemNotFound);
}
