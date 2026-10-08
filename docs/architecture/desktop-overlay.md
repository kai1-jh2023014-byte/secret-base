# Desktop Overlay

Secret Base is an **overlay Desktop Environment layer** on top of the normal Windows desktop.

It does **not** replace Explorer, the Taskbar, or the Windows shell.

## Goal

```text
Desktop
  ├── Wallpaper / Desktop icons     ← receive input outside widget regions
  ├── Secret Base Overlay           ← HWND shaped to widgets + FABs (SetWindowRgn)
  │     ├── Widgets (Clock, Text, Web, Calendar, Music, Creative, AI, …)
  │     ├── Blocks
  │     └── FABs (+ Add Widget, Blk, Aa, ⚙) + taskbar shelf (focus, next, AI)
  └── Other application windows / Taskbar / Windows Search (unchanged)
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
| `GetShellWindow` + `SetWindowPos` insert-after the window above that shell window | Keep widgets above the wallpaper and under other top-level apps |
| Transparent page / canvas brushes | XAML layer stays clear |

All of the above are public Windows App SDK / documented Win32 APIs in `IDesktopOverlayService` → `AppWindowDesktopOverlayService`.

Investigation notes: [overlay-input-and-edges.md](overlay-input-and-edges.md).

## Input policy (widgets vs Desktop)

- App computes widget (+ optional debug chrome) client rects in physical pixels.
- Platform applies documented `SetWindowRgn` so the HWND shape is the union of those rects.
- The same region is also applied to WinUI's `DesktopChildSiteBridge` child when present (island input surface).
- Regions are cached and reapplied when the overlay is parked just above the shell desktop after activation.
- Outside the region, Explorer / other apps receive mouse input (cross-process).
- `WM_NCHITTEST` / `HTTRANSPARENT` alone is **not** used for Desktop passthrough (same-thread limitation per Win32 docs).

## Z-order policy

- Overlay is **not** always-on-top.
- On configure and on each activation, Platform calls documented `GetShellWindow` and `SetWindowPos` so the overlay is inserted immediately above the shell desktop (`SWP_NOACTIVATE`). The window passed as insert-after is the one already above the shell; that window stays in front of the overlay.
- `HWND_BOTTOM` is only the fallback when the shell window cannot be resolved. On Windows 10/11 it places the host behind the wallpaper, so the process looks like it flashed and closed.
- Result: widgets sit above the wallpaper, **under** normal applications.
- Does **not** use Explorer WorkerW / shell subclassing.

## UI chrome (widgets-only)

| Element | Normal UX | Developer recovery |
|---------|-----------|--------------------|
| Page / canvas background | Transparent | — |
| Title / brand text | Hidden | — |
| Control | Normal UX | Recovery |
|---------|-----------|----------|
| **+** Add Widget | Always visible (catalog) | Debug chrome **Add Widget**, **Ctrl+Shift+N** |
| **Blk** Add Block | Always visible | Debug chrome **Add Block**, **Ctrl+Shift+B** |
| Per-type shortcuts | W/C/M/E/A still add Web/Cal/Music/Creative/AI | See [keyboard-shortcuts.md](../guides/keyboard-shortcuts.md) |
| **Aa** Theme | Always visible | Debug chrome **Theme**, **Ctrl+Shift+T** |
| **⚙** Setup | Always visible | Start Menu / Desktop shortcut / Start at login |
| Host status | Ephemeral message above FABs | Also mirrored in debug chrome |
| **Taskbar shelf** | Centered bottom shelf (focus, next item, AI field). Keeps clear of the left FAB strip. Clock/time stays on the Clock widget | **Ctrl+Shift+K** focuses the AI field. Does **not** replace the Windows taskbar or Windows Search |
| Status / widget count | Hidden | **Ctrl+Shift+D** shows debug chrome |
| Exit button | Hidden | Debug chrome **Exit**, or **Ctrl+Shift+Q** |
| `WidgetFrame` | Grip + resize + **×** remove (Progress minimal: hidden until selected) | Same |

Desktop-wide Grid Arrange FAB was removed. Per-Block icon Arrange (inside a Block header) remains.

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
