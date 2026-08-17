using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Classroom;
using SecretBase.Core.Creative;
using SecretBase.Core.Music;

namespace SecretBase.Core.Integration;

/// <summary>
/// Routes catalog command ids to existing Command services.
/// Does not replace Music/Creative/Ai services. Does not talk to OS.
/// </summary>
public sealed class IntegrationCommandService
{
    private readonly CalendarCommandService? _calendar;
    private readonly MusicCommandService? _music;
    private readonly CreativeCommandService? _creative;
    private readonly ClassroomCommandService _classroom;
    private readonly AppCommandService? _apps;
    private readonly AiCommandService? _ai;

    public IntegrationCommandService(
        CalendarCommandService? calendar = null,
        MusicCommandService? music = null,
        CreativeCommandService? creative = null,
        ClassroomCommandService? classroom = null,
        AppCommandService? apps = null,
        AiCommandService? ai = null)
    {
        _calendar = calendar;
        _music = music;
        _creative = creative;
        _classroom = classroom ?? new ClassroomCommandService();
        _apps = apps;
        _ai = ai;
    }

    public async Task<IntegrationCommandResult> ExecuteAsync(
        IntegrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var id = request.CommandId?.Trim() ?? string.Empty;
        if (IntegrationCatalog.FindById(id) is null)
        {
            return IntegrationCommandResult.Fail(id, "Unknown integration command.");
        }

        return id switch
        {
            IntegrationCommandIds.CalendarGetTodayEvents => await CalendarAsync(
                id, CalendarCommand.GetTodayEvents(), cancellationToken).ConfigureAwait(false),
            IntegrationCommandIds.CalendarRefresh => await CalendarAsync(
                id, CalendarCommand.Refresh(), cancellationToken).ConfigureAwait(false),
            IntegrationCommandIds.CalendarOpen => await CalendarAsync(
                id, CalendarCommand.Open(), cancellationToken).ConfigureAwait(false),
            IntegrationCommandIds.MusicSearch => await MusicAsync(
                id, MusicCommand.SearchTrack(request.Query ?? string.Empty), cancellationToken)
                .ConfigureAwait(false),
            IntegrationCommandIds.MusicPlay =>
                IntegrationCommandResult.Fail(id, "Play requires a resolved MusicCommand.PlayTrack from the Music Widget."),
            IntegrationCommandIds.MusicPause => await MusicAsync(
                id, MusicCommand.Pause(), cancellationToken).ConfigureAwait(false),
            IntegrationCommandIds.MusicNext => await MusicAsync(
                id, MusicCommand.Next(), cancellationToken).ConfigureAwait(false),
            IntegrationCommandIds.ClassroomOpen => Classroom(id, ClassroomCommand.Open()),
            IntegrationCommandIds.ClassroomRefresh => Classroom(id, ClassroomCommand.Refresh()),
            IntegrationCommandIds.ClassroomGetAssignments => Classroom(id, ClassroomCommand.GetAssignments()),
            IntegrationCommandIds.CreativeListProjects => CreativeList(id),
            IntegrationCommandIds.CreativeOpenProject => CreativeOpen(id, request.ProjectId),
            IntegrationCommandIds.AppsListApps => AppsList(id),
            IntegrationCommandIds.AppsOpenApp => AppsOpen(id, request.AppId),
            IntegrationCommandIds.CursorOpenProject => CursorOpen(id, request.ProjectId),
            _ => IntegrationCommandResult.Fail(id, "Unknown integration command.")
        };
    }

    private async Task<IntegrationCommandResult> CalendarAsync(
        string id,
        CalendarCommand command,
        CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return IntegrationCommandResult.Fail(id, "Calendar is not wired.");
        }

        var result = await _calendar.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? IntegrationCommandResult.Ok(
                id,
                shouldLaunch: result.ShouldLaunch,
                launchTarget: result.LaunchTarget,
                launchIsExternalLink: result.LaunchIsExternalLink,
                events: result.Events)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Calendar command failed.");
    }

    private async Task<IntegrationCommandResult> MusicAsync(
        string id,
        MusicCommand command,
        CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return IntegrationCommandResult.Fail(id, "Music is not wired.");
        }

        var result = await _music.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? IntegrationCommandResult.Ok(id)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Music command failed.");
    }

    private IntegrationCommandResult Classroom(string id, ClassroomCommand command)
    {
        var result = _classroom.Execute(command);
        return result.Succeeded
            ? IntegrationCommandResult.Ok(
                id,
                shouldLaunch: result.ShouldLaunch,
                launchTarget: result.LaunchTarget,
                launchIsExternalLink: result.LaunchIsExternalLink)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Classroom command failed.");
    }

    private IntegrationCommandResult CreativeList(string id)
    {
        if (_creative is null)
        {
            return IntegrationCommandResult.Fail(id, "Creative is not wired.");
        }

        var result = _creative.Execute(CreativeCommand.SearchProjects(null));
        return result.Succeeded
            ? IntegrationCommandResult.Ok(id, projects: result.Projects)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Could not list projects.");
    }

    private IntegrationCommandResult CreativeOpen(string id, string? projectId)
    {
        if (_creative is null)
        {
            return IntegrationCommandResult.Fail(id, "Creative is not wired.");
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            return IntegrationCommandResult.Fail(id, "projectId is required.");
        }

        var result = _creative.Execute(CreativeCommand.OpenCreativeProject(projectId));
        return result.Succeeded
            ? IntegrationCommandResult.Ok(id, projects: result.Project is null ? [] : [result.Project])
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Could not open project.");
    }

    private IntegrationCommandResult AppsList(string id)
    {
        if (_apps is null)
        {
            return IntegrationCommandResult.Fail(id, "Apps are not wired.");
        }

        var result = _apps.Execute(AppCommand.ListApps());
        return result.Succeeded
            ? IntegrationCommandResult.Ok(id, apps: result.Apps)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Could not list apps.");
    }

    private IntegrationCommandResult AppsOpen(string id, string? appId)
    {
        if (_apps is null)
        {
            return IntegrationCommandResult.Fail(id, "Apps are not wired.");
        }

        if (string.IsNullOrWhiteSpace(appId))
        {
            return IntegrationCommandResult.Fail(id, "appId is required.");
        }

        var result = _apps.Execute(AppCommand.OpenApp(appId));
        return result.Succeeded
            ? IntegrationCommandResult.Ok(
                id,
                shouldLaunch: result.ShouldLaunch,
                launchTarget: result.LaunchTarget,
                launchIsExternalLink: result.LaunchIsExternalLink,
                apps: result.App is null ? [] : [result.App])
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Could not open app.");
    }

    private IntegrationCommandResult CursorOpen(string id, string? projectId)
    {
        if (_ai is null)
        {
            return IntegrationCommandResult.Fail(id, "Cursor integration is not wired.");
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            return IntegrationCommandResult.Fail(id, "projectId is required.");
        }

        var result = _ai.Execute(AiCommand.OpenProjectInCursor(projectId));
        return result.Succeeded
            ? IntegrationCommandResult.Ok(
                id,
                shouldOpenCursorAtFolder: result.ShouldOpenCursorAtFolder,
                cursorFolderPath: result.FolderPath)
            : IntegrationCommandResult.Fail(id, result.ErrorMessage ?? "Could not open project in Cursor.");
    }
}
