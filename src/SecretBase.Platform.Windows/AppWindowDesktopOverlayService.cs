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

    // Last interactive rects (client-relative physical px) — reapplied after Z-order / DWM churn.
    private readonly object _regionGate = new();
    private nint _regionHwnd;
    private OverlayInputRect[] _lastRects = [];

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
            // FRAMECHANGED after DWM attrs so border/corner preference sticks.
            _ = NativeMethods.SetWindowPos(
                target.WindowHandle,
                nint.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNozorder
                | NativeMethods.SwpNoactivate | NativeMethods.SwpFramechanged);
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

        // WinUI/DWM may reset window regions around activation — reapply cached shape.
        TrySuppressSystemEdgeChrome(target.WindowHandle);
        ReapplyCachedInteractiveRegions(target.WindowHandle);
    }

    public void UpdateInteractiveInputRegions(
        DesktopOverlayTarget target,
        IReadOnlyList<OverlayInputRect> rects)
    {
        if (target.WindowHandle == nint.Zero)
        {
            return;
        }

        var snapshot = rects.ToArray();
        lock (_regionGate)
        {
            _regionHwnd = target.WindowHandle;
            _lastRects = snapshot;
        }

        ApplyInteractiveRegions(target.WindowHandle, snapshot);
    }

    private void ReapplyCachedInteractiveRegions(nint hwnd)
    {
        OverlayInputRect[] snapshot;
        lock (_regionGate)
        {
            if (_regionHwnd != hwnd || _lastRects.Length == 0)
            {
                return;
            }

            snapshot = _lastRects;
        }

        ApplyInteractiveRegions(hwnd, snapshot);
    }

    private void ApplyInteractiveRegions(nint topLevelHwnd, IReadOnlyList<OverlayInputRect> rects)
    {
        // Documented user32 SetWindowRgn: window shape = interactive widgets only.
        // Outside the region, input goes to Desktop / other processes (cross-process).
        //
        // WinUI 3: hit-testing often lives on the DesktopChildSiteBridge child HWND.
        // Apply the same client-relative region to the top-level window AND that bridge
        // (castorix / microsoft-ui-xaml#10746). Coordinates: SetWindowRgn is relative to
        // each HWND's upper-left; for a fill-client bridge that matches our client rects.
        var targets = EnumerateRegionTargetHwnds(topLevelHwnd);
        foreach (var hwnd in targets)
        {
            ApplyRegionToHwnd(hwnd, rects);
        }
    }

    private static List<nint> EnumerateRegionTargetHwnds(nint topLevelHwnd)
    {
        var list = new List<nint> { topLevelHwnd };
        var bridges = new List<nint>();
        NativeMethods.EnumChildWindows(
            topLevelHwnd,
            (child, _) =>
            {
                var className = GetWindowClassName(child);
                if (className.Contains("DesktopChildSiteBridge", StringComparison.OrdinalIgnoreCase)
                    || className.Contains("DesktopWindowXamlSource", StringComparison.OrdinalIgnoreCase)
                    || className.Contains("InputSiteWindow", StringComparison.OrdinalIgnoreCase))
                {
                    bridges.Add(child);
                }

                return true;
            },
            0);

        list.AddRange(bridges);
        return list;
    }

    private static string GetWindowClassName(nint hwnd)
    {
        var buffer = new System.Text.StringBuilder(256);
        var length = NativeMethods.GetClassName(hwnd, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : string.Empty;
    }

    private static void ApplyRegionToHwnd(nint hwnd, IReadOnlyList<OverlayInputRect> rects)
    {
        // After a successful SetWindowRgn, the system owns the HRGN — do not DeleteObject it.
        nint combined = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        if (combined == nint.Zero)
        {
            return;
        }

        var transferred = false;
        var pieces = 0;
        try
        {
            foreach (var rect in rects)
            {
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    continue;
                }

                // Map App-supplied client px → this HWND's window-relative px.
                if (!TryMapClientRectToWindowRgn(hwnd, rect, out var left, out var top, out var right, out var bottom))
                {
                    left = rect.X;
                    top = rect.Y;
                    right = rect.X + rect.Width;
                    bottom = rect.Y + rect.Height;
                }

                var piece = NativeMethods.CreateRectRgn(left, top, right, bottom);
                if (piece == nint.Zero)
                {
                    continue;
                }

                _ = NativeMethods.CombineRgn(combined, combined, piece, NativeMethods.RgnOr);
                _ = NativeMethods.DeleteObject(piece);
                pieces++;
            }

            // An empty region hides the whole window (the shortcut appears, then vanishes).
            // Leave the previous shape until real widget rects exist.
            if (pieces == 0)
            {
                return;
            }

            if (NativeMethods.SetWindowRgn(hwnd, combined, redraw: true))
            {
                transferred = true;
            }
        }
        finally
        {
            if (!transferred && combined != nint.Zero)
            {
                _ = NativeMethods.DeleteObject(combined);
            }
        }
    }

    /// <summary>
    /// Converts a top-level client rectangle into coordinates suitable for SetWindowRgn on
    /// <paramref name="hwnd"/> (window-relative, including any non-client offset).
    /// </summary>
    private static bool TryMapClientRectToWindowRgn(
        nint hwnd,
        OverlayInputRect clientRect,
        out int left,
        out int top,
        out int right,
        out int bottom)
    {
        left = top = right = bottom = 0;

        // Treat incoming rect as relative to the top-level client; map via screen space.
        var topLevel = NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot) is var root and not 0
            ? root
            : hwnd;

        var topLeft = new NativeMethods.Point { X = clientRect.X, Y = clientRect.Y };
        var bottomRight = new NativeMethods.Point
        {
            X = clientRect.X + clientRect.Width,
            Y = clientRect.Y + clientRect.Height
        };

        if (!NativeMethods.ClientToScreen(topLevel, ref topLeft)
            || !NativeMethods.ClientToScreen(topLevel, ref bottomRight))
        {
            return false;
        }

        if (!NativeMethods.GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        left = topLeft.X - windowRect.Left;
        top = topLeft.Y - windowRect.Top;
        right = bottomRight.X - windowRect.Left;
        bottom = bottomRight.Y - windowRect.Top;
        return right > left && bottom > top;
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

        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DwmwaCaptionColor,
            ref colorNone,
            sizeof(uint));

        // Avoid rounded-corner anti-alias halo on a work-area transparent host.
        var corner = NativeMethods.DwmwcpDoNotRound;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DwmwaWindowCornerPreference,
            ref corner,
            sizeof(uint));

        // Prefer no system backdrop material on the HWND chrome (Win11).
        var backdropNone = NativeMethods.DwmsbtNone;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DwmwaSystemBackdropType,
            ref backdropNone,
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
            // Region cache is per-service instance; subclass is static — cannot reach instance.
            // KeepBehind / next App Sync will reapply. FRAMECHANGED helps DWM attrs stick.
            _ = NativeMethods.SetWindowPos(
                hWnd,
                nint.Zero,
                0,
                0,
                0,
                0,
                NativeMethods.SwpNomove | NativeMethods.SwpNosize | NativeMethods.SwpNozorder
                | NativeMethods.SwpNoactivate | NativeMethods.SwpFramechanged);
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
        public const uint SwpNozorder = 0x0004;
        public const uint SwpNoactivate = 0x0010;
        public const uint SwpFramechanged = 0x0020;

        public const uint DwmBbEnable = 0x00000001;
        public const uint DwmBbBlurRegion = 0x00000002;

        public const uint WmEraseBkgnd = 0x0014;
        public const uint WmDwmCompositionChanged = 0x031E;
        public const nuint EraseSubclassId = 1;

        // DWMWINDOWATTRIBUTE (Win11 Build 22000+)
        public const uint DwmwaWindowCornerPreference = 33;
        public const uint DwmwaBorderColor = 34;
        public const uint DwmwaCaptionColor = 35;
        public const uint DwmwaSystemBackdropType = 38;
        public const uint DwmwaColorNone = 0xFFFFFFFE;
        public const uint DwmwcpDoNotRound = 1;
        public const uint DwmsbtNone = 1;

        public const int RgnOr = 2;
        public const uint GaRoot = 2;

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
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        public delegate nint SubclassProc(
            nint hWnd,
            uint uMsg,
            nint wParam,
            nint lParam,
            nuint uIdSubclass,
            nuint dwRefData);

        public delegate bool EnumChildProc(nint hWnd, nint lParam);

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
        public static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        public static extern bool ClientToScreen(nint hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        public static extern nint GetAncestor(nint hWnd, uint gaFlags);

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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(nint hWndParent, EnumChildProc lpEnumFunc, nint lParam);

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
