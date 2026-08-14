# Secret Base Architecture Overview

Secret Base is an **overlay-style Personal Desktop Environment** for Windows.

Highest principle:

> If Secret Base breaks, Windows itself must remain intact.

## Layers

| Project | Responsibility |
|---------|----------------|
| `SecretBase.App` | WinUI shell, Desktop host UI, composition root |
| `SecretBase.Core` | Domain models (Desktop/Room/Security). No Windows API calls |
| `SecretBase.Infrastructure` | Logging, AppData paths, JSON layout/theme persistence |
| `SecretBase.Platform.Abstractions` | OS-facing contracts (`ICompatibilityService`, `ISafeExitService`, `IDesktopOverlayService`) |
| `SecretBase.Platform.Windows` | Windows adapter implementations only (AppWindow overlay, Safe Exit, compatibility) |
| `SecretBase.Widgets` | Built-in widget views (Clock, Text, Web, Calendar) + theme painting helpers |

## Why this split

- Windows Update / API churn is isolated in `Platform.Windows`.
- Core stays testable without UI or OS hooks.
- Future Plugin / AI code cannot "reach through" Core into Win32 by accident.

## Process model (v0.1)

- Unpackaged WinUI 3 app (`WindowsPackageType=None`).
- Closing the window / Exit button ends the process only.
- No Explorer hooks, no Taskbar replacement, no shell registry mutation.

## Next milestones

- ✅ Clock Widget (reference implementation)
- ✅ Text Widget
- ✅ Desktop Overlay (chromeless work-area host; widgets-only UX)
- ✅ Widget-shaped input (`SetWindowRgn`) + DWM edge suppress (see overlay-input-and-edges.md)
- ✅ Block rooms (layout schema v2; launch via Platform `ITargetLaunchService`)
- ✅ Theme editor (presets + colors for Clock / Text / Blocks)
- ✅ Desktop arrange (**Grid** FAB — even widget/block placement)
- ✅ Web Widget (WebView2; Untrusted; no host bridge)
- ✅ Calendar Widget (Today agenda + `ICalendarProvider` / Google ICS)
- App Launcher polish → Room switching

