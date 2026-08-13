# Desktop Overlay

Secret Base is an **overlay Desktop Environment layer** on top of the normal Windows desktop.

It does **not** replace Explorer, the Taskbar, or the Windows shell.

## Goal

```text
Desktop
  ├── Wallpaper / Desktop icons     ← receive input outside widget regions
  ├── Secret Base Overlay           ← HWND shaped to widgets only (SetWindowRgn)
  │     ├── Clock Widget            ← receives input
  │     └── Text Widget             ← receives input
  └── Other application windows / Taskbar
```

Normal UX: **widgets only**. Transparent/empty areas pass input to Windows. No title, status strip, or Exit button on the wallpaper.

## Public APIs used (v0.1 overlay)

| API | Purpose |
|-----|---------|
| `OverlappedPresenter.SetBorderAndTitleBar(false, false)` | Remove system border + title bar |
| `OverlappedPresenter` flags | Chromeless, non-topmost host |
| `DisplayArea.WorkArea` + `AppWindow.MoveAndResize` | Fit work area (above Taskbar) |
| `AppWindow.IsShownInSwitchers` | Keep Alt+Tab discoverability for Safe Exit |
| Transparent `SystemBackdrop` (`Windows.UI.Composition` brush) | Clear host fill for wallpaper |
| `DwmExtendFrameIntoClientArea` | Documented DWM frame into client |
| `DwmEnableBlurBehindWindow` + empty region | Documented DWM glass clear |
| `DwmSetWindowAttribute(DWMWA_BORDER_COLOR, COLOR_NONE)` | Suppress Win11 thin white frame |
| `DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE, DONOTROUND)` | Avoid corner AA halo |
| `SetWindowSubclass` + `WM_ERASEBKGND` FillRect | Skip opaque client erase (black→glass) |
| `SetWindowRgn` (union of widget client rects) | Cross-process click-through outside widgets |
| `SetWindowRgn` on `DesktopChildSiteBridge` child | WinUI island hit-test surface (in addition to top-level HWND) |
| `SetWindowPos(..., HWND_BOTTOM, ...)` | Keep overlay under other top-level apps |
| Transparent page / canvas brushes | XAML layer stays clear |

All of the above are public Windows App SDK / documented Win32 APIs in `IDesktopOverlayService` → `AppWindowDesktopOverlayService`.

Investigation notes: [overlay-input-and-edges.md](overlay-input-and-edges.md).

## Input policy (widgets vs Desktop)

- App computes widget (+ optional debug chrome) client rects in physical pixels.
- Platform applies documented `SetWindowRgn` so the HWND shape is the union of those rects.
- The same region is also applied to WinUI's `DesktopChildSiteBridge` child when present (island input surface).
- Regions are cached and reapplied when the overlay is parked at `HWND_BOTTOM` after activation.
- Outside the region, Explorer / other apps receive mouse input (cross-process).
- `WM_NCHITTEST` / `HTTRANSPARENT` alone is **not** used for Desktop passthrough (same-thread limitation per Win32 docs).

## Z-order policy

- Overlay is **not** always-on-top.
- On configure and on each activation, Platform calls documented `SetWindowPos` with `HWND_BOTTOM` + `SWP_NOACTIVATE`.
- Result: widgets sit above the wallpaper, **under** normal applications.
- Does **not** use Explorer WorkerW / shell subclassing.

## UI chrome (widgets-only)

| Element | Normal UX | Developer recovery |
|---------|-----------|--------------------|
| Page / canvas background | Transparent | — |
| Title / brand text | Hidden | — |
| **+** Add Block | Always visible (bottom-left FAB) | Debug chrome **Add Block** |
| **Web** Add Web Widget | Always visible | Debug chrome **Add Web**, **Ctrl+Shift+W** |
| **Cal** Add Calendar | Always visible | Debug chrome **Add Cal**, **Ctrl+Shift+C** |
| **Aa** Theme | Always visible | Debug chrome **Theme**, **Ctrl+Shift+T** |
| **Grid** Arrange | Always visible — even layout for widgets & blocks | Debug chrome **Arrange** |
| Status / widget count | Hidden | **Ctrl+Shift+D** shows debug chrome |
| Exit button | Hidden | Debug chrome **Exit**, or **Ctrl+Shift+Q** |
| `WidgetFrame` | Subtle grip + resize (stronger on hover) | Same |

Arrange uses Core `DesktopWidgetLayout` / `DesktopBlockLayout` (compact equal-gap grid from top-left; Blocks sit below the widget band) and persists via the layout store.

## Explicitly out of scope / forbidden

- Explorer.exe injection / subclassing / WorkerW tricks
- Undocumented Taskbar COM shell replacement
- Shell DLL patching / registry shell mutation
- Admin elevation
- Replacing the Windows shell

## Known limitations (honest)

- Hit regions are axis-aligned (widget corner radius is visual only).
- Keyboard accelerators need focus (click a widget or Alt+Tab) after interacting with the Desktop.
- Host still sizes to primary `WorkArea` (multi-monitor follow-up).
- Win10 may ignore Win11-only DWM border attributes (harmless).

## Safe Exit

- **Ctrl+Shift+Q** — when Secret Base has keyboard focus
- Debug chrome **Exit** — after **Ctrl+Shift+D**
- Alt+Tab / taskbar still list Secret Base (process exit leaves Explorer intact)
