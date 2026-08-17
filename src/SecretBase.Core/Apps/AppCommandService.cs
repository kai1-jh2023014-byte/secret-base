using SecretBase.Core.Ai;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Apps;

/// <summary>
/// Routes My Apps commands. Does not launch. Host uses ITargetLaunchService / ICursorLaunchService.
/// </summary>
public sealed class AppCommandService
{
    private readonly CustomAppService _apps;
    private readonly CreativeProjectService? _projects;

    public AppCommandService(CustomAppService apps, CreativeProjectService? projects = null)
    {
        _apps = apps ?? throw new ArgumentNullException(nameof(apps));
        _projects = projects;
    }

    public CustomAppService Apps => _apps;

    public AppCommandResult Execute(AppCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command.Kind switch
        {
            AppCommandKind.ListApps => AppCommandResult.Ok(AppCommandKind.ListApps, apps: _apps.List()),
            AppCommandKind.OpenApp => OpenApp(command),
            AppCommandKind.OpenAppInCursor => OpenInCursor(command),
            _ => AppCommandResult.Fail(command.Kind, "Unknown app command.")
        };
    }

    private AppCommandResult OpenApp(AppCommand command)
    {
        if (!_apps.TryGet(command.AppId, out var app) || app is null)
        {
            return AppCommandResult.Fail(AppCommandKind.OpenApp, "App is not registered.");
        }

        return AppCommandResult.Ok(
            AppCommandKind.OpenApp,
            app: app,
            shouldLaunch: true,
            launchTarget: app.LaunchTarget,
            launchIsExternalLink: app.Type == CustomAppType.Website);
    }

    private AppCommandResult OpenInCursor(AppCommand command)
    {
        if (!_apps.TryGet(command.AppId, out var app) || app is null)
        {
            return AppCommandResult.Fail(AppCommandKind.OpenAppInCursor, "App is not registered.");
        }

        var folder = app.ProjectRoot;
        if (string.IsNullOrWhiteSpace(folder)
            && !string.IsNullOrWhiteSpace(app.CreativeProjectId)
            && _projects is not null)
        {
            folder = _projects.FindById(app.CreativeProjectId)?.RootFolder;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return AppCommandResult.Fail(
                AppCommandKind.OpenAppInCursor,
                "No project root is registered for this app.");
        }

        if (!AiCursorFolderValidator.TryNormalizeProjectRoot(folder, out var normalized, out var error))
        {
            return AppCommandResult.Fail(AppCommandKind.OpenAppInCursor, error);
        }

        return AppCommandResult.Ok(
            AppCommandKind.OpenAppInCursor,
            app: app,
            shouldOpenCursorAtFolder: true,
            cursorFolderPath: normalized);
    }
}
