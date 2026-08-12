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
| `SecretBase.Widgets` | Built-in widget views (Clock, Text) + theme painting helpers |

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
- ✅ Desktop Overlay (chromeless work-area host; public AppWindow APIs)
- App Launcher → Drag polish → Theme editor → WebContent → Room switching
 → click-through empty regions (follow-up)

