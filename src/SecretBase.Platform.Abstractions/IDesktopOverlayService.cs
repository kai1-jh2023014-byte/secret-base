namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Configures the Secret Base host window as a chromeless Desktop Environment overlay.
/// Implementations must use public Windows App SDK / documented Win32 APIs only and must
/// never touch Explorer, Taskbar, shell DLLs, or the registry.
/// </summary>
public interface IDesktopOverlayService
{
    /// <summary>
    /// Applies borderless / title-bar-less presentation and sizes the window to the
    /// display work area (excludes the Windows taskbar). Enables documented DWM
    /// transparent-frame setup so wallpaper can show through empty areas.
    /// </summary>
    void ApplyChromelessWorkAreaOverlay(DesktopOverlayTarget target);

    /// <summary>
    /// Places the overlay at the bottom of the top-level Z-order (documented SetWindowPos /
    /// HWND_BOTTOM) so normal apps stay above widgets, while the overlay remains above the
    /// desktop wallpaper layer. Does not use Explorer WorkerW or shell hooks.
    /// </summary>
    void KeepBehindApplicationWindows(DesktopOverlayTarget target);
}
