using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using SecretBase.Platform.Abstractions;
using Windows.Graphics;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Desktop overlay host using public Windows App SDK windowing APIs + documented DWM/user32.
/// Does not modify Explorer, Taskbar, shell state, or the registry.
/// </summary>
public sealed class AppWindowDesktopOverlayService : IDesktopOverlayService
{
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setwindowpos
    private static readonly nint HwndBottom = 1;

    public void ApplyChromelessWorkAreaOverlay(DesktopOverlayTarget target)
    {
        var windowId = new WindowId(target.AppWindowId);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Title = "Secret Base";
        // Keep Alt+Tab / taskbar presence so Safe Exit remains discoverable without shell hooks.
        appWindow.IsShownInSwitchers = true;

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = false;
        appWindow.SetPresenter(presenter);

        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        appWindow.MoveAndResize(new RectInt32(work.X, work.Y, work.Width, work.Height));

        if (target.WindowHandle != nint.Zero)
        {
            TryEnableTransparentFrame(target.WindowHandle);
            KeepBehindApplicationWindows(target);
        }
    }

    public void KeepBehindApplicationWindows(DesktopOverlayTarget target)
    {
        if (target.WindowHandle == nint.Zero)
        {
            return;
        }

        // Documented user32 SetWindowPos: park at bottom of Z-order without activating.
        _ = NativeMethods.SetWindowPos(
            target.WindowHandle,
            HwndBottom,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNoactivate);
    }

    private static void TryEnableTransparentFrame(nint hwnd)
    {
        // Public dwmapi: extend frame into client so empty regions can be see-through.
        var margins = new NativeMethods.MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Public dwmapi: empty blur region clears the black client fill WinUI otherwise paints.
        // Same documented approach used by transparent WinUI host samples (not Explorer hooks).
        var hrgn = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        try
        {
            var bb = new NativeMethods.DwmBlurBehind
            {
                dwFlags = NativeMethods.DwmBbEnable | NativeMethods.DwmBbBlurRegion,
                fEnable = true,
                hRgnBlur = hrgn,
                fTransitionOnMaximized = false
            };
            _ = NativeMethods.DwmEnableBlurBehindWindow(hwnd, ref bb);
        }
        finally
        {
            if (hrgn != nint.Zero)
            {
                _ = NativeMethods.DeleteObject(hrgn);
            }
        }
    }

    private static class NativeMethods
    {
        public const uint SwpNosize = 0x0001;
        public const uint SwpNomove = 0x0002;
        public const uint SwpNoactivate = 0x0010;

        public const uint DwmBbEnable = 0x00000001;
        public const uint DwmBbBlurRegion = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DwmBlurBehind
        {
            public uint dwFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fEnable;
            public nint hRgnBlur;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fTransitionOnMaximized;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmEnableBlurBehindWindow(nint hwnd, ref DwmBlurBehind pBlurBehind);

        [DllImport("gdi32.dll")]
        public static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        public static extern int DeleteObject(nint hObject);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
    }
}
