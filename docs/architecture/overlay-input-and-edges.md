# Overlay input passthrough & edge frame — investigation

Date: 2026-08-12  
Branch context: `cursor/overlay-ux-widgets-only-6d90`  
Scope: investigate before large changes; public APIs only.

## 1. Current Overlay structure

```text
App
 └─ MainWindow (one HWND / AppWindow)
     ├─ TransparentSystemBackdrop (Windows.UI.Composition clear brush)
     ├─ IDesktopOverlayService.ApplyChromelessWorkAreaOverlay
     │    ├─ OverlappedPresenter.SetBorderAndTitleBar(false,false)
     │    ├─ DisplayArea.WorkArea → AppWindow.MoveAndResize
     │    ├─ DwmExtendFrameIntoClientArea(-1)
     │    ├─ DwmEnableBlurBehindWindow(empty region)
     │    ├─ SetWindowSubclass (WM_ERASEBKGND / WM_DWMCOMPOSITIONCHANGED)
     │    └─ SetWindowPos(HWND_BOTTOM)
     └─ RootFrame → DesktopPage
          └─ RootGrid / WidgetCanvas (all Transparent)
               └─ WidgetFrame[] (DIP Canvas.Left/Top + Width/Height)
```

Secret Base owns **one work-area-sized top-level window**. Transparency is visual only; the HWND still hit-tests the full client.

## 2. Current Window Bounds

Configured once at startup in `AppWindowDesktopOverlayService`:

- Source: `DisplayArea.GetFromWindowId(..., Primary).WorkArea`
- Applied: `AppWindow.MoveAndResize(work)`
- **Not** using `DisplayArea.OuterBounds` (full monitor including taskbar)
- No display-change / DPI-change listener to re-apply bounds

So Window Bounds ≈ **primary monitor work area** (excludes taskbar), in display coordinates from WASDK.

## 3. Current Content Bounds

XAML fill chain has **no** margin/padding:

`RootFrame` → `DesktopPage` → `RootGrid` → `WidgetCanvas` → `WidgetFrame`

Content Bounds track the window client area. Widget geometry is DIP on the canvas (`WidgetInstance.Position` / `Size`), converted to physical pixels only if Platform needs it (`XamlRoot.RasterizationScale`).

## 4. White edge — ranked causes

| Rank | Cause | Evidence in this repo |
|------|--------|------------------------|
| 1 | Win11 DWM **visible frame border** still drawn after chromeless presenter | `SetBorderAndTitleBar(false,false)` only; **no** `DwmSetWindowAttribute(DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE)` |
| 2 | Win11 **rounded corners** anti-alias halo on a transparent full-area window | No `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND` |
| 3 | Glass stack fringe (`DwmExtendFrameIntoClientArea(-1)` + empty blur + transparent backdrop) | Present; can lighten outer edge on some builds |
| 4 | WorkArea vs OuterBounds strip | Expected gap above taskbar — not a thin white *frame* on all four sides |
| 5 | DPI `RectInt32` 1px shortfall | Possible but secondary; PerMonitorV2 is set |
| 6 | XAML inset | Unlikely — no margins on root tree |

**Most actionable fix:** documented `DwmSetWindowAttribute` for `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE` (0xFFFFFFFE) and `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND`, isolated in `Platform.Windows`.

## 5. Click-through candidates

### A. Documented Win32 in `Platform.Windows` (recommended)

| Technique | Cross-process to Desktop/apps? | Notes |
|-----------|--------------------------------|-------|
| **`SetWindowRgn`** union of widget client rects | **Yes** | Documented user32/gdi32. Window shape = interactive widgets only. Matches “widgets exist on desktop” UX. |
| `WM_NCHITTEST` → `HTTRANSPARENT` | **No (insufficient alone)** | Docs: HTTRANSPARENT only continues to windows **in the same thread**. Explorer/Desktop are other processes. |
| `WS_EX_TRANSPARENT` | Whole window only | Widgets would also lose input unless combined with other tricks. |
| `WS_EX_LAYERED` + `UpdateLayeredWindow` | Conflicts with WinUI composition / current backdrop | Poor fit. |
| Per-widget `AppWindow` | Yes | Larger redesign; deferred. |

**Isolation:** App converts widget DIP → client physical rects; `IDesktopOverlayService.UpdateInteractiveInputRegions(...)` applies `SetWindowRgn` in Platform.Windows. Core stays free of Win32.

### B. WinUI / WASDK only

`IsHitTestVisible`, transparent brushes, non-client pointer APIs **cannot** pass clicks to other processes while keeping one full work-area HWND. Transparency ≠ input passthrough.

### C. Keep as known limitation

Already documented. Safe, but fails the target UX (“transparent giant window” vs “widgets only”).

## 6. Safety comparison

| Option | Safety vs project boundaries |
|--------|------------------------------|
| A `SetWindowRgn` + DWM border attrs | **High** — documented APIs, Platform-only, no Explorer/WorkerW/admin/registry |
| A `HTTRANSPARENT` only | Safe APIs but **wrong capability** for Desktop |
| B | Safe but **cannot** meet UX |
| C | Safest code-wise; UX incomplete |
| Forbidden paths | WorkerW / Taskbar COM / shell inject / undocumented APIs — **rejected** |

## 7. Windows Update resilience

| Option | Resilience |
|--------|------------|
| A `SetWindowRgn` + `DwmSetWindowAttribute` | **High** — long-standing / documented Win11 DWM attrs; fails soft if attribute unsupported |
| B | N/A (doesn't solve) |
| C | Highest (no new surface) |
| Shell hacks | Brittle — forbidden |

## 8. Recommendation

1. **Problem 2 (white frame):** apply `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE` and `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND` in `AppWindowDesktopOverlayService`.
2. **Problem 1 (input):** **Option A** with **`SetWindowRgn`** driven by widget (+ optional debug chrome) client rects via new Abstractions API. Do **not** rely on `HTTRANSPARENT` for Desktop passthrough.
3. Do **not** use WorkerW / Explorer / Taskbar COM.
4. Defer per-widget windows until region approach proves insufficient.

## 9. Files to change (minimal)

| File | Change |
|------|--------|
| `docs/architecture/overlay-input-and-edges.md` | This investigation |
| `docs/architecture/desktop-overlay.md` | Document region + DWM border policy |
| `docs/decisions/README.md` | Decision entry |
| `Platform.Abstractions/IDesktopOverlayService.cs` | `UpdateInteractiveInputRegions` |
| `Platform.Abstractions/OverlayInputRect.cs` | Plain rect DTO |
| `Platform.Windows/AppWindowDesktopOverlayService.cs` | DWM border attrs + `SetWindowRgn` |
| `App/App.xaml.cs` / `MainWindow.xaml.cs` | Pass overlay target into page args |
| `App/DesktopPage.xaml.cs` | Compute rects + sync on layout |
| `App/Desktop/WidgetFrame.xaml.cs` | Notify bounds changes during drag/resize |

## 10. Minimal implementation plan

1. Suppress DWM border / rounded halo (Problem 2).
2. Add `OverlayInputRect` + `UpdateInteractiveInputRegions`.
3. After widget render / move / resize / debug-chrome toggle, App pushes rects; Platform sets region.
4. Keep `HWND_BOTTOM`, Safe Exit, layout persistence.
5. Verify on Windows x64: widget click/drag, desktop click-through, other apps, edges, Exit, layout restore.

### Honest limitations after A

- Hit regions are **axis-aligned** (rounded widget corners still square for input).
- Keyboard accelerators need focus (Alt+Tab / click a widget) if the last click was on the desktop.
- Multi-monitor: still primary `WorkArea` only until a follow-up.
- Win10 may ignore Win11-only border color attribute (harmless HRESULT).
