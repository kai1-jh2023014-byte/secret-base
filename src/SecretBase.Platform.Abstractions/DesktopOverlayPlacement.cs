namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Chooses the documented <c>SetWindowPos</c> insert-after handle for the overlay.
/// <c>HWND_BOTTOM</c> places the window behind the shell desktop, so a running
/// process looks like it flashed and closed. The insert-after window is the one
/// that stays in front of the overlay; passing the window already above the shell
/// desktop parks widgets just above the wallpaper and still under other apps.
/// </summary>
public readonly record struct DesktopOverlayPlacement(nint InsertAfter, bool Change)
{
    /// <summary><c>HWND_BOTTOM</c> ((HWND)1).</summary>
    public const nint HwndBottom = 1;

    /// <summary><c>HWND_TOP</c> ((HWND)0).</summary>
    public const nint HwndTop = 0;

    public static DesktopOverlayPlacement Unchanged { get; } = new(0, false);

    public static DesktopOverlayPlacement AboveDesktop(
        nint shellWindow,
        nint windowAboveShell,
        nint ourWindow,
        bool windowAboveShellIsTopMost)
    {
        if (shellWindow == nint.Zero)
        {
            return new DesktopOverlayPlacement(HwndBottom, true);
        }

        if (windowAboveShell == nint.Zero)
        {
            // The desktop window is already the front non-topmost window.
            return new DesktopOverlayPlacement(HwndTop, true);
        }

        if (windowAboveShell == ourWindow || windowAboveShellIsTopMost)
        {
            // Already directly above the desktop, or the next window is topmost
            // (inserting after it would promote this overlay).
            return Unchanged;
        }

        return new DesktopOverlayPlacement(windowAboveShell, true);
    }
}
