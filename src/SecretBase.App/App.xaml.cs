using Microsoft.UI.Xaml;
using SecretBase.Core;
using SecretBase.Core.Creative;
using SecretBase.Core.Time;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
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

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _logger = new FileAppLogger(AppDataPaths.LogsDirectory);
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
        var creativeCommands = new CreativeCommandService(
            new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()));

        var pageArgs = new DesktopPageArgs(
            Logger: _logger,
            SafeExit: _safeExit,
            Compatibility: info,
            LayoutStore: new JsonLayoutStore(),
            ThemeStore: new JsonThemeStore(),
            TimeProvider: new SystemTimeProvider(),
            Launcher: new ShellTargetLaunchService(),
            Icons: new ShellFileIconService(AppDataPaths.IconsDirectory),
            Intake: new BlockItemIntakeService(AppDataPaths.BlockItemsDirectory),
            PathPicker: pathPicker,
            CreativeCommands: creativeCommands);

        IDesktopOverlayService overlay = new AppWindowDesktopOverlayService();
        _window = new MainWindow(pageArgs, overlay);
        _window.Closed += (_, _) =>
        {
            _logger?.Info("lifecycle", "Main window closed. Returning to normal Windows desktop.");
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
        _window.Activate();
        _logger.Info("overlay", "Host activated; Blocks + widgets; SetWindowRgn input; HWND_BOTTOM Z-order.");
    }
}
