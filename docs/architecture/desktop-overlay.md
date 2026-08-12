# Desktop Overlay

Secret Base is an **overlay Desktop Environment layer** on top of the normal Windows desktop.

It does **not** replace Explorer, the Taskbar, or the Windows shell.

## Goal

```text
Desktop
  ├── Wallpaper          ← visible through transparent overlay
  ├── Secret Base Overlay
  │     ├── Clock Widget ← floating
  │     └── Text Widget
  └── Other app windows / Taskbar
```

Normal UX: **widgets only**. No title, status strip, or Exit button on the wallpaper.

## Public APIs used (v0.1 overlay)

| API | Purpose |
|-----|---------|
| `OverlappedPresenter.SetBorderAndTitleBar(false, false)` | Remove system border + title bar |
| `OverlappedPresenter` flags (`IsResizable` / min / max / always-on-top) | Chromeless, non-topmost host |
| `DisplayArea.GetFromWindowId` + `WorkArea` | Size to the work area (above the Taskbar) |
| `AppWindow.MoveAndResize` | Apply work-area bounds |
| `AppWindow.IsShownInSwitchers` | Keep Alt+Tab / taskbar entry for Safe Exit discoverability |
| Custom `SystemBackdrop` (transparent brush) | Transparent host backdrop |
| Documented `DwmExtendFrameIntoClientArea` (`dwmapi.dll`) | Allow wallpaper to show through empty client area |

All of the above are public Windows App SDK / documented Win32 APIs. They live behind `IDesktopOverlayService` → `AppWindowDesktopOverlayService` in `SecretBase.Platform.Windows`.

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
- Click-through of transparent pixels (follow-up; not in this milestone)

## Click-through limitation (honest)

WinUI 3 does **not** currently expose a first-party “pass clicks through transparent pixels” API.

This milestone therefore:

- Makes the host **visually** transparent / chromeless
- Fits the **work area** (does not cover the Taskbar)
- Keeps **IsAlwaysOnTop = false** so other apps stay usable above the overlay
- Does **not** implement WS_EX_TRANSPARENT / custom `WM_NCHITTEST` hit-testing yet

Empty overlay regions may still receive input while Secret Base is the topmost window in Z-order.

## Safe Exit

- **Ctrl+Shift+Q** — always available
- Debug chrome **Exit** — after **Ctrl+Shift+D**
- Alt+Tab / taskbar still list Secret Base (process exit leaves Explorer intact)
