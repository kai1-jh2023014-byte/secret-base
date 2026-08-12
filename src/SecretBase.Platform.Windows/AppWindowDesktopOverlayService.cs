using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using SecretBase.Platform.Abstractions;
using Windows.Graphics;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Desktop overlay host using public Windows App SDK windowing APIs (+ documented DWM for transparency).
/// Does not modify Explorer, Taskbar, shell state, or the registry.
/// </summary>
public sealed class AppWindowDesktopOverlayService : IDesktopOverlayService
{
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
        // Not always-on-top: other apps stay usable above the overlay layer.
        presenter.IsAlwaysOnTop = false;
        appWindow.SetPresenter(presenter);

        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        appWindow.MoveAndResize(new RectInt32(work.X, work.Y, work.Width, work.Height));

        // Documented dwmapi: extend frame so a transparent SystemBackdrop can show through.
        if (target.WindowHandle != nint.Zero)
        {
            TryEnableTransparentFrame(target.WindowHandle);
        }
    }

    private static void TryEnableTransparentFrame(nint hwnd)
    {
        // Public DWM API (dwmapi.dll). Negative margins extend the frame into the full client area.
        var margins = new NativeMethods.MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [DllImport("dwmapi.dll")]
        internal static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);
    }
}
