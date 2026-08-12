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
    /// display work area (excludes the Windows taskbar).
    /// </summary>
    void ApplyChromelessWorkAreaOverlay(DesktopOverlayTarget target);
}
