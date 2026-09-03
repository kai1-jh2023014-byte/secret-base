using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Storage;
using SecretBase.Platform.Mac;

namespace SecretBase.App.Mac;

public partial class App : Avalonia.Application
{
    private IDisposable? _singleInstance;
    private FileAppLogger? _logger;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            FileAppLogger? bootstrapLogger = null;
            try
            {
                bootstrapLogger = new FileAppLogger(AppDataPaths.LogsDirectory);
                var guard = new FileLockSingleInstanceGuard();
                if (!guard.TryAcquire())
                {
                    bootstrapLogger.Info("startup", "Another Secret Base instance is already running. Exiting.");
                    guard.Dispose();
                    bootstrapLogger.Dispose();
                    desktop.Shutdown(0);
                    return;
                }

                _singleInstance = guard;
                _logger = bootstrapLogger;
                var session = MacHostComposer.Create(_logger, () => desktop.Shutdown(0));
                var window = new MainWindow(session);
                desktop.MainWindow = window;
                desktop.Exit += (_, _) =>
                {
                    _logger?.Info("lifecycle", "Main window closed. Finder / Dock untouched.");
                    _singleInstance?.Dispose();
                    _singleInstance = null;
                    _logger?.Dispose();
                    _logger = null;
                };
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

                desktop.Shutdown(1);
                return;
            }
        }

        base.OnFrameworkInitializationCompleted();
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
