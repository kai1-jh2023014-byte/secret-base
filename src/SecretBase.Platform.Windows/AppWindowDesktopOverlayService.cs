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

    // Keep subclass proc / GDI brush alive for the process lifetime.
    private static NativeMethods.SubclassProc? s_eraseSubclassProc;
    private static readonly HashSet<nint> s_subclassedHwnds = [];
    private static nint s_blackBrush;

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
            TrySuppressSystemEdgeChrome(target.WindowHandle);
            TrySubclassEraseBackground(target.WindowHandle);
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

    public void UpdateInteractiveInputRegions(
        DesktopOverlayTarget target,
        IReadOnlyList<OverlayInputRect> rects)
    {
        if (target.WindowHandle == nint.Zero)
        {
            return;
        }

        // Documented user32 SetWindowRgn: window shape = interactive widgets only.
        // Outside the region, input goes to Desktop / other processes (cross-process).
        // After a successful SetWindowRgn, the system owns the HRGN — do not DeleteObject it.
        nint combined = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        if (combined == nint.Zero)
        {
            return;
        }

        try
        {
            foreach (var rect in rects)
            {
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    continue;
                }

                var left = rect.X;
                var top = rect.Y;
                var right = rect.X + rect.Width;
                var bottom = rect.Y + rect.Height;
                var piece = NativeMethods.CreateRectRgn(left, top, right, bottom);
                if (piece == nint.Zero)
                {
                    continue;
                }

                _ = NativeMethods.CombineRgn(combined, combined, piece, NativeMethods.RgnOr);
                _ = NativeMethods.DeleteObject(piece);
            }

            if (!NativeMethods.SetWindowRgn(target.WindowHandle, combined, redraw: true))
            {
                _ = NativeMethods.DeleteObject(combined);
            }

            // Ownership transferred to the system on success.
            combined = nint.Zero;
        }
        finally
        {
            if (combined != nint.Zero)
            {
                _ = NativeMethods.DeleteObject(combined);
            }
        }
    }

    private static void TryEnableTransparentFrame(nint hwnd)
    {
        // Public dwmapi: extend frame into client (full client glass for wallpaper visibility).
        var margins = new NativeMethods.MARGINS
        {
            cxLeftWidth = -1,
            cxRightWidth = -1,
            cyTopHeight = -1,
            cyBottomHeight = -1
        };
        _ = NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);

        // Public dwmapi: empty blur region + transparent Windows.UI SystemBackdrop clears
        // the opaque black client WinUI paints by default.
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

    private static void TrySuppressSystemEdgeChrome(nint hwnd)
    {
        // Win11+: suppress the thin DWM border that remains after SetBorderAndTitleBar(false,false).
        // https://learn.microsoft.com/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute
        var colorNone = NativeMethods.DwmwaColorNone;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DwmwaBorderColor,
            ref colorNone,
            sizeof(uint));

        // Avoid rounded-corner anti-alias halo on a work-area transparent host.
        var corner = NativeMethods.DwmwcpDoNotRound;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DwmwaWindowCornerPreference,
            ref corner,
            sizeof(uint));
    }

    private static void TrySubclassEraseBackground(nint hwnd)
    {
        if (!s_subclassedHwnds.Add(hwnd))
        {
            return;
        }

        if (s_blackBrush == nint.Zero)
        {
            // COLORREF 0 (black) + DWM glass → see-through; WinUIEx TransparentTintBackdrop pattern.
            s_blackBrush = NativeMethods.CreateSolidBrush(0);
        }

        s_eraseSubclassProc ??= EraseBackgroundSubclass;
        if (!NativeMethods.SetWindowSubclass(hwnd, s_eraseSubclassProc, NativeMethods.EraseSubclassId, 0))
        {
            s_subclassedHwnds.Remove(hwnd);
        }
    }

    private static nint EraseBackgroundSubclass(
        nint hWnd,
        uint uMsg,
        nint wParam,
        nint lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        if (uMsg == NativeMethods.WmEraseBkgnd)
        {
            if (TryClearClient(hWnd, wParam))
            {
                return 1;
            }
        }
        else if (uMsg == NativeMethods.WmDwmCompositionChanged)
        {
            TryEnableTransparentFrame(hWnd);
            TrySuppressSystemEdgeChrome(hWnd);
            return 0;
        }

        return NativeMethods.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private static bool TryClearClient(nint hwnd, nint hdc)
    {
        if (!NativeMethods.GetClientRect(hwnd, out var rect))
        {
            return false;
        }

        if (s_blackBrush == nint.Zero)
        {
            s_blackBrush = NativeMethods.CreateSolidBrush(0);
        }

        _ = NativeMethods.FillRect(hdc, ref rect, s_blackBrush);
        return true;
    }

    private static class NativeMethods
    {
        public const uint SwpNosize = 0x0001;
        public const uint SwpNomove = 0x0002;
        public const uint SwpNoactivate = 0x0010;

        public const uint DwmBbEnable = 0x00000001;
        public const uint DwmBbBlurRegion = 0x00000002;

        public const uint WmEraseBkgnd = 0x0014;
        public const uint WmDwmCompositionChanged = 0x031E;
        public const nuint EraseSubclassId = 1;

        // DWMWINDOWATTRIBUTE (Win11 Build 22000+)
        public const uint DwmwaWindowCornerPreference = 33;
        public const uint DwmwaBorderColor = 34;
        public const uint DwmwaColorNone = 0xFFFFFFFE;
        public const uint DwmwcpDoNotRound = 1;

        public const int RgnOr = 2;

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

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public delegate nint SubclassProc(
            nint hWnd,
            uint uMsg,
            nint wParam,
            nint lParam,
            nuint uIdSubclass,
            nuint dwRefData);

        [DllImport("dwmapi.dll")]
        public static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        public static extern int DwmEnableBlurBehindWindow(nint hwnd, ref DwmBlurBehind pBlurBehind);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(
            nint hwnd,
            uint dwAttribute,
            ref uint pvAttribute,
            int cbAttribute);

        [DllImport("gdi32.dll")]
        public static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

        [DllImport("gdi32.dll")]
        public static extern int CombineRgn(nint hrgnDest, nint hrgnSrc1, nint hrgnSrc2, int iMode);

        [DllImport("gdi32.dll")]
        public static extern nint CreateSolidBrush(uint colorRef);

        [DllImport("gdi32.dll")]
        public static extern int DeleteObject(nint hObject);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(nint hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        public static extern int FillRect(nint hDC, ref Rect lprc, nint hbr);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowRgn(nint hWnd, nint hRgn, bool redraw);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
            nint hWnd,
            nint hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        // https://learn.microsoft.com/windows/win32/api/commctrl/nf-commctrl-setwindowsubclass
        [DllImport("comctl32.dll", SetLastError = true)]
        public static extern bool SetWindowSubclass(
            nint hWnd,
            SubclassProc pfnSubclass,
            nuint uIdSubclass,
            nuint dwRefData);

        [DllImport("comctl32.dll")]
        public static extern nint DefSubclassProc(nint hWnd, uint uMsg, nint wParam, nint lParam);
    }
}
