# Desktop Overlay

Secret Base is an **overlay Desktop Environment layer** on top of the normal Windows desktop.

It does **not** replace Explorer, the Taskbar, or the Windows shell.

## Goal

```text
Desktop
  ├── Wallpaper                 ← visible through transparent overlay
  ├── Secret Base Overlay       ← below other apps (HWND_BOTTOM)
  │     ├── Clock Widget
  │     └── Text Widget
  └── Other application windows / Taskbar
```

Normal UX: **widgets only**. No title, status strip, or Exit button on the wallpaper.

## Public APIs used (v0.1 overlay)

| API | Purpose |
|-----|---------|
| `OverlappedPresenter.SetBorderAndTitleBar(false, false)` | Remove system border + title bar |
| `OverlappedPresenter` flags | Chromeless, non-topmost host |
| `DisplayArea.WorkArea` + `AppWindow.MoveAndResize` | Fit work area (above Taskbar) |
| `AppWindow.IsShownInSwitchers` | Keep Alt+Tab discoverability for Safe Exit |
| Transparent `SystemBackdrop` (ABI cast brush) | Clear host fill for wallpaper |
| `DwmExtendFrameIntoClientArea` | Documented DWM frame into client |
| `DwmEnableBlurBehindWindow` + empty region | Documented DWM clear of black client fill |
| `SetWindowPos(..., HWND_BOTTOM, ...)` | Keep overlay under other top-level apps |
| Transparent page / canvas brushes | XAML layer stays clear |

All of the above are public Windows App SDK / documented Win32 APIs in `IDesktopOverlayService` → `AppWindowDesktopOverlayService`.

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
| Status / widget count | Hidden | **Ctrl+Shift+D** shows debug chrome |
| Exit button | Hidden | Debug chrome **Exit**, or **Ctrl+Shift+Q** |
| `WidgetFrame` | Subtle grip + resize (stronger on hover) | Same |

## Explicitly out of scope / forbidden

- Explorer.exe injection / subclassing / WorkerW tricks
- Undocumented Taskbar COM shell replacement
- Shell DLL patching / registry shell mutation
- Admin elevation
- Replacing the Windows shell
- Click-through of transparent pixels (follow-up)

## Click-through limitation (honest)

Empty overlay regions may still receive input while Secret Base occupies that hit-test region. Pixel click-through is deferred.

## Safe Exit

- **Ctrl+Shift+Q** — always available
- Debug chrome **Exit** — after **Ctrl+Shift+D**
- Alt+Tab / taskbar still list Secret Base (process exit leaves Explorer intact)
