# Overlay input passthrough & edge frame — investigation

Date: 2026-08-12 (initial) · **2026-08-13 (re-check after Windows reports)**  
Branches: `cursor/overlay-ux-widgets-only-6d90` → hardening on `cursor/overlay-hit-test-hardening-6d90`  
Scope: public APIs only; no Explorer / WorkerW / Taskbar COM / undocumented APIs.

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
     │    ├─ DwmSetWindowAttribute(BORDER/CAPTION COLOR_NONE, DONOTROUND, SYSTEMBACKDROP NONE)
     │    ├─ SetWindowPos(SWP_FRAMECHANGED)
     │    ├─ SetWindowSubclass (WM_ERASEBKGND / WM_DWMCOMPOSITIONCHANGED)
     │    └─ SetWindowPos(HWND_BOTTOM)
     └─ RootFrame → DesktopPage
          └─ RootGrid / WidgetCanvas (Transparent)
               └─ WidgetFrame[] + FABs
     └─ child HWND: Microsoft.UI.Content.DesktopChildSiteBridge  ← WinUI input surface
```

Secret Base owns **one work-area-sized top-level window**. Visual transparency ≠ input passthrough.

## 2. Current Window Bounds

Configured at startup in `AppWindowDesktopOverlayService`:

- Source: `DisplayArea.GetFromWindowId(..., Primary).WorkArea`
- Applied: `AppWindow.MoveAndResize(work)`
- **Not** `DisplayArea.OuterBounds` (excludes taskbar — intentional)
- No display-change listener yet (known follow-up)

Window Bounds ≈ primary monitor **work area**.

## 3. Current Content Bounds

XAML fill chain has **no** margin/padding:

`RootFrame` → `DesktopPage` → `RootGrid` → `WidgetCanvas`

Content Bounds track the client area. Widget geometry is DIP; App converts to physical client px via `XamlRoot.RasterizationScale` → `OverlayInputRect`.

## 4. White edge — ranked causes

| Rank | Cause | Status |
|------|--------|--------|
| 1 | Win11 DWM **visible frame border** after chromeless presenter | Mitigated: `DWMWA_BORDER_COLOR = COLOR_NONE` (+ caption NONE) |
| 2 | Win11 **rounded corners** AA halo | Mitigated: `DWMWCP_DONOTROUND` + `SWP_FRAMECHANGED` |
| 3 | System backdrop material fringe | Mitigated: `DWMWA_SYSTEMBACKDROP_TYPE = NONE` |
| 4 | Glass stack (`DwmExtendFrameIntoClientArea(-1)`) | Present (needed for wallpaper show-through) |
| 5 | WorkArea vs OuterBounds strip above taskbar | Expected gap — not a 4-side thin frame |
| 6 | XAML inset | Unlikely — no root margins |

**Important:** If `SetWindowRgn` shapes the HWND to widgets only, the **work-area perimeter white frame disappears** because those pixels are outside the window region. Persistent full-screen edge chrome strongly suggests the region was **not** taking effect on the HWND that actually hit-tests.

## 5. Click-through candidates

### A. Documented Win32 in `Platform.Windows` (**recommended / in use**)

| Technique | Cross-process? | Notes |
|-----------|----------------|-------|
| **`SetWindowRgn`** union of widget client rects | **Yes** | Documented user32/gdi32. Proven on WinUI 3 (microsoft-ui-xaml#10746) when applied correctly. |
| Apply RGN to **DesktopChildSiteBridge** child | **Yes** | castorix: WinUI input often lives on this child — top-level-only RGN can look like a no-op |
| `WM_NCHITTEST` → `HTTRANSPARENT` | **No alone** | Same-thread only; Desktop is another process |
| `WS_EX_TRANSPARENT` | Whole window | Widgets lose input too |
| `WS_EX_LAYERED` + color key | Conflicts with WinUI composition | Rejected for host shell |
| Per-widget `AppWindow` | Yes | Larger redesign; deferred |

### B. WinUI / WASDK only

`IsHitTestVisible` / transparent brushes **cannot** pass clicks to other processes while keeping one full work-area HWND.

### C. Keep as known limitation

Safe code-wise; fails target UX (“widgets only on Desktop”).

## 6. Safety

| Option | vs project boundaries |
|--------|------------------------|
| A `SetWindowRgn` (+ child bridge) + DWM attrs | **High** — documented, Platform-only |
| B | Safe but cannot meet UX |
| C | Safest; UX incomplete |
| WorkerW / Taskbar COM / shell inject | **Forbidden** |

## 7. Windows Update resilience

| Option | Resilience |
|--------|------------|
| A `SetWindowRgn` + documented DWM attrs | **High**; Win11-only attrs fail soft |
| Child class name `DesktopChildSiteBridge` | **Medium** — string is implementation detail of WinUI island; we also try related class names and always set top-level |
| Shell hacks | Brittle — forbidden |

## 8. Recommendation

1. Stay on **Option A** (already chosen).
2. **Hardening (this change):** apply `SetWindowRgn` to top-level **and** `DesktopChildSiteBridge` (and similar) children; map client→window coords; cache + reapply on `HWND_BOTTOM` / activation; strengthen DWM edge suppress (`CAPTION_COLOR`, `SYSTEMBACKDROP_TYPE`, `SWP_FRAMECHANGED`); post-island sync from App.
3. Do **not** use WorkerW / Explorer / Taskbar COM.
4. Defer per-widget windows unless hardening fails on Windows.

## 9. Files changed (hardening)

| File | Change |
|------|--------|
| `Platform.Windows/AppWindowDesktopOverlayService.cs` | Child HWND RGN, coord map, cache/reapply, DWM extras |
| `App/MainWindow.xaml.cs` | Post-navigate region sync |
| `App/DesktopPage.xaml.cs` | `RequestInteractiveRegionSync` |
| `docs/architecture/overlay-input-and-edges.md` | This re-investigation |
| `docs/architecture/desktop-overlay.md` | Bridge note |

## 10. Minimal implementation plan

1. Investigate (done) — Option A already present; Windows symptoms ⇒ RGN likely missing on island HWND / getting cleared.
2. Harden Platform apply path (child bridge + reapply + DWM).
3. Re-sync after island creation.
4. Verify on Windows x64: widget click/drag, Desktop click-through, other apps, edges, Exit, layout restore.

### Honest limitations after A

- Hit regions are **axis-aligned**.
- Keyboard accelerators need focus after clicking Desktop.
- Multi-monitor: primary `WorkArea` only.
- If WinUI renames the island class, child targeting may miss — top-level RGN remains as fallback.
- Win10 may ignore some Win11 DWM attributes (harmless).

### Modal dialogs (Settings / onboarding)

`ContentDialog` is centered on the full work-area HWND. While a dialog is open, the host must temporarily expand the hit region via `BeginModalInput` / `OverlayDialogInput` (full-window `SetWindowRgn`), then restore widget-only regions in `EndModalInput`.

**Important:** While a modal is open (`_modalInputDepth > 0`), `SyncInteractiveInputRegions` must **not** shrink the region back to widgets. Doing so (e.g. via `ShowHostStatus` or ToggleSwitch side effects) makes the dialog unreachable and looks like a freeze.
