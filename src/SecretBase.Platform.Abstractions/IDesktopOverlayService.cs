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
    /// Parks the overlay just above the shell desktop window (documented GetShellWindow +
    /// SetWindowPos) so normal apps stay above widgets and the wallpaper stays behind them.
    /// HWND_BOTTOM is only the fallback when the shell window cannot be resolved — it sits
    /// behind the desktop and looks like an immediate exit. Does not use Explorer WorkerW
    /// or shell hooks.
    /// </summary>
    void KeepBehindApplicationWindows(DesktopOverlayTarget target);

    /// <summary>
    /// Restricts the overlay HWND shape/hit-test to the union of <paramref name="rects"/>
    /// via documented <c>SetWindowRgn</c>. Empty/outside areas pass input to Desktop and
    /// other apps. Rects are client-relative physical pixels supplied by the App layer.
    /// </summary>
    void UpdateInteractiveInputRegions(
        DesktopOverlayTarget target,
        IReadOnlyList<OverlayInputRect> rects);
}
