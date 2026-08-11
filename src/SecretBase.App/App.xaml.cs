using Microsoft.UI.Xaml;
using SecretBase.Core;
using SecretBase.Infrastructure.Logging;
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

        _window = new MainWindow(_logger, _safeExit, info);
        _window.Closed += (_, _) =>
        {
            _logger?.Info("lifecycle", "Main window closed. Returning to normal Windows desktop.");
            if (_logger is IDisposable disposable)
            {
                disposable.Dispose();
            }
        };
        _window.Activate();
    }
}
