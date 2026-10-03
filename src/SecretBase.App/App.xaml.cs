using Microsoft.UI.Xaml;
using SecretBase.Core;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Creative;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Startup;
using SecretBase.Infrastructure.Storage;
using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Windows;

namespace SecretBase.App;

public partial class App : Application
{
    private Window? _window;
    private IAppLogger? _logger;
    private ICompatibilityService? _compatibility;
    private ISafeExitService? _safeExit;
    private ISingleInstanceGuard? _singleInstance;
    private WindowsInstanceActivation? _activation;

    public App()
    {
        TryWriteAttempt();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        FileAppLogger? bootstrapLogger = null;
        var isAutoStartLaunch = StartupLaunchMode.IsAutoStartLaunch;
        try
        {
            bootstrapLogger = new FileAppLogger(AppDataPaths.LogsDirectory);
            if (isAutoStartLaunch)
            {
                bootstrapLogger.Info("startup", "Auto-start launch detected.");
            }
            var guard = new WindowsMutexSingleInstanceGuard();
            _singleInstance = guard;
            if (!guard.TryAcquire())
            {
                var mutexError = guard.FailureMessage;
                var alreadyVisible = string.Equals(
                    StartupTrace.ReadStatus(AppDataPaths.LogsDirectory),
                    StartupTrace.VisibleStatus,
                    StringComparison.Ordinal);
                bootstrapLogger.Info(
                    "startup",
                    string.IsNullOrWhiteSpace(mutexError)
                        ? "Another Secret Base instance is already running. Asking it to show."
                        : "Single-instance lock could not be opened. " + mutexError);
                if (!alreadyVisible || !string.IsNullOrWhiteSpace(mutexError))
                {
                    TryWriteStartupStatus(
                        string.IsNullOrWhiteSpace(mutexError) ? "already-running" : "mutex-error",
                        mutexError);
                }
                if (!string.IsNullOrWhiteSpace(mutexError))
                {
                    ShowLaunchNotice(
                        "Secret Base could not take its single-instance lock, so this launch closed."
                        + Environment.NewLine + Environment.NewLine
                        + mutexError
                        + Environment.NewLine + Environment.NewLine
                        + "End SecretBase.App.exe in Task Manager, then open the desktop shortcut again."
                        + Environment.NewLine
                        + TracePath());
                }
                else
                {
                    var signaled = WindowsInstanceActivation.Signal();
                    if (!signaled || !alreadyVisible)
                    {
                        ShowLaunchNotice(
                            "Secret Base is already running, so this launch closed."
                            + Environment.NewLine + Environment.NewLine
                            + "If no widgets are on the desktop, the previous window is behind the wallpaper or still starting."
                            + " Open Task Manager, end SecretBase.App.exe, then open the desktop shortcut again."
                            + Environment.NewLine + Environment.NewLine
                            + TracePath());
                    }
                }

                _singleInstance.Dispose();
                _singleInstance = null;
                bootstrapLogger.Dispose();
                Exit();
                return;
            }

            TryWriteStartupStatus("process-start", null);
            RunStartup(bootstrapLogger);
        }
        catch (Exception ex)
        {
            try
            {
                bootstrapLogger ??= TryCreateBootstrapLogger();
                bootstrapLogger?.Error("startup", "Secret Base could not start.", ex);
            }
            catch
            {
                // Logging must not prevent shutdown.
            }

            TryWriteStartupStatus("failed", ex.ToString());

            try
            {
                var silent = StartupLaunchMode.IsAutoStartLaunch;
                var shown = !silent && ShowLaunchNotice(
                    "Secret Base could not start."
                    + Environment.NewLine + Environment.NewLine
                    + ex.Message
                    + Environment.NewLine + Environment.NewLine
                    + "Your data was not deleted."
                    + Environment.NewLine
                    + "Data folder: " + AppDataPaths.RootDirectory
                    + Environment.NewLine
                    + TracePath());
                if (!shown)
                {
                    StartupFailurePresenter.ShowBlocking(ex.Message, AppDataPaths.RootDirectory, silentUi: silent);
                }
            }
            catch
            {
                // Dialog failure must not prevent shutdown.
            }

            try
            {
                Exit();
            }
            catch
            {
                Environment.Exit(1);
            }
        }
    }

    private static bool ShowLaunchNotice(string message)
    {
        if (StartupLaunchMode.IsAutoStartLaunch)
        {
            return false;
        }

        try
        {
            WindowsUserNotice.Show(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string TracePath() =>
        "Log: " + Path.Combine(AppDataPaths.LogsDirectory, StartupTrace.FileName);

    private static void TryWriteAttempt()
    {
        try
        {
            StartupTrace.WriteAttempt(AppDataPaths.LogsDirectory);
        }
        catch
        {
            // A missing trace must not block startup.
        }
    }

    private static void TryWriteStartupStatus(string status, string? detail)
    {
        try
        {
            StartupTrace.Write(AppDataPaths.LogsDirectory, status, detail);
        }
        catch
        {
            // A missing trace must not block startup.
        }
    }

    private void RunStartup(FileAppLogger logger)
    {
        _logger = logger;
        _compatibility = new WindowsCompatibilityService(windowsAppSdkPackageVersion: "2.3.1");
        _safeExit = new ProcessSafeExitService(() =>
        {
            _logger?.Info("lifecycle", "Safe exit requested — process will terminate; Windows shell untouched.");
            _window?.Close();
        });

        var info = _compatibility.GetCurrent();
        _logger.Info("startup", $"{AppInfo.Name} v{AppInfo.Version} starting.");
        _logger.Info(
            "compatibility",
            $"OS={info.OsDescription}; OSVersion={info.OsVersion}; Arch={info.OsArchitecture}; DotNet={info.DotNetVersion}; WASDK={info.WindowsAppSdkPackageVersion}");

        var pathPicker = new WindowsPathPickService();
        var cursorLaunch = new WindowsCursorLaunchService();
        var launchSettingsStore = new JsonAppLaunchSettingsStore();
        var autoStart = new WindowsRegistryAutoStartService();
        AutoStartCoordinator.SynchronizeAtStartup(autoStart, launchSettingsStore, _logger);
        var projectService = new CreativeProjectService(new JsonCreativeProjectStore());
        var aiCommands = new AiCommandService(
            AiWorkspaceWidgetConfiguration.CreateDefault(),
            projectService,
            () => cursorLaunch.IsAvailable);
        var creativeCommands = new CreativeCommandService(
            new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()),
            projectService,
            aiCommands);
        var appCommands = new AppCommandService(
            new CustomAppService(new JsonCustomAppStore()),
            projectService);

        var pageArgs = new DesktopPageArgs(
            Logger: _logger,
            SafeExit: _safeExit,
            Compatibility: info,
            LayoutStore: new JsonLayoutStore(logger: _logger),
            ThemeStore: new JsonThemeStore(logger: _logger),
            TimeProvider: new SystemTimeProvider(),
            Launcher: new ShellTargetLaunchService(),
            Icons: new ShellFileIconService(AppDataPaths.IconsDirectory),
            Intake: new BlockItemIntakeService(AppDataPaths.BlockItemsDirectory),
            PathPicker: pathPicker,
            CustomIcons: new WindowsCustomIconService(AppDataPaths.CustomIconsDirectory),
            CreativeCommands: creativeCommands,
            CursorLaunch: cursorLaunch,
            AiCommands: aiCommands,
            AppCommands: appCommands,
            AutoStart: autoStart,
            LaunchSettingsStore: launchSettingsStore);

        IDesktopOverlayService overlay = new AppWindowDesktopOverlayService();
        _window = new MainWindow(pageArgs, overlay);
        _window.Closed += (_, _) =>
        {
            _logger?.Info("lifecycle", "Main window closed. Returning to normal Windows desktop.");
            _activation?.Dispose();
            _activation = null;
            _singleInstance?.Dispose();
            _singleInstance = null;
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
        _window.Activate();
        if (_window is MainWindow mainWindow)
        {
            _activation = WindowsInstanceActivation.Listen(() =>
                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    mainWindow.PresentExistingInstance();
                    TryWriteStartupStatus(StartupTrace.VisibleStatus, null);
                }));
        }

        _logger.Info("overlay", "Host activated; Blocks + widgets; SetWindowRgn input; z-order above the shell desktop.");
        TryWriteStartupStatus(StartupTrace.VisibleStatus, null);
    }

    private static FileAppLogger? TryCreateBootstrapLogger()
    {
        try
        {
            return new FileAppLogger(AppDataPaths.LogsDirectory);
        }
        catch
        {
            return null;
        }
    }
}
