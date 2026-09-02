using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App;

/// <summary>
/// Keeps JSON launch preference and the Windows Startup Apps registry entry aligned.
/// </summary>
internal static class AutoStartCoordinator
{
    public static void SynchronizeAtStartup(
        IAutoStartService autoStart,
        IAppLaunchSettingsStore settingsStore,
        IAppLogger? logger)
    {
        if (!autoStart.IsSupported)
        {
            return;
        }

        var settings = settingsStore.LoadOrCreate();
        var status = autoStart.GetStatus();
        var registryOn = status.IsRegistered && status.PointsToCurrentExecutable;

        if (settings.LaunchAtWindowsLogin)
        {
            if (status.IsRegistered && !status.PointsToCurrentExecutable)
            {
                if (autoStart.TryEnable(out var repairError))
                {
                    logger?.Info("startup", "Auto-start registration updated for the current executable.");
                }
                else
                {
                    logger?.Warn("startup", $"Auto-start repair failed: {repairError}");
                }
            }
            else if (!status.IsRegistered)
            {
                settings.LaunchAtWindowsLogin = false;
                settingsStore.Save(settings);
                logger?.Info("startup", "Auto-start preference cleared (disabled in Windows Startup apps).");
            }
        }
        else if (!settings.LaunchAtWindowsLogin)
        {
            if (registryOn)
            {
                settings.LaunchAtWindowsLogin = true;
                settingsStore.Save(settings);
                logger?.Info("startup", "Auto-start enabled from Windows Startup apps.");
            }
            else if (status.IsRegistered)
            {
                if (autoStart.TryDisable(out var disableError))
                {
                    logger?.Info("startup", "Removed stale Windows auto-start registration.");
                }
                else
                {
                    logger?.Warn("startup", $"Auto-start cleanup failed: {disableError}");
                }
            }
        }
    }

    public static bool TrySetEnabled(
        bool enabled,
        IAutoStartService autoStart,
        IAppLaunchSettingsStore settingsStore,
        IAppLogger? logger,
        out string? errorMessage)
    {
        errorMessage = null;
        var settings = settingsStore.LoadOrCreate();

        if (enabled)
        {
            if (!autoStart.TryEnable(out errorMessage))
            {
                return false;
            }
        }
        else if (!autoStart.TryDisable(out errorMessage))
        {
            return false;
        }

        settings.LaunchAtWindowsLogin = enabled;
        settingsStore.Save(settings);
        logger?.Info("startup", enabled ? "Auto-start enabled by user." : "Auto-start disabled by user.");
        return true;
    }
}
