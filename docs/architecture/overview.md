# Secret Base Architecture Overview

Secret Base is an **overlay-style Personal Creative Desktop Environment** for Windows.

Highest principle:

> If Secret Base breaks, Windows itself must remain intact.

## Layers

| Project | Responsibility |
|---------|----------------|
| `SecretBase.App` | WinUI shell, Desktop host UI, composition root |
| `SecretBase.Core` | Domain models, Commands, validation. No Windows API calls |
| `SecretBase.Infrastructure` | Logging, AppData paths, JSON persistence |
| `SecretBase.Platform.Abstractions` | OS-facing contracts |
| `SecretBase.Platform.Windows` | Overlay HWND, Safe Exit, launch, Cursor discovery |
| `SecretBase.Widgets` | Built-in widget views + theme painting |

## Process model (v0.1)

- Unpackaged WinUI 3 app (`WindowsPackageType=None`).
- Closing / Safe Exit ends the process only after layout save.
- No Explorer hooks, no Taskbar replacement, no shell registry mutation.

## v0.1 surface

- ✅ Desktop Overlay (chromeless work-area; widgets-only click-through)
- ✅ Widgets: Clock, Text, Web, Calendar, Music, Creative (Projects + Dashboard), AI
- ✅ Blocks (launch tiles)
- ✅ Theme + Arrange
- ✅ Projects → Dashboard → Cursor / ChatGPT / resources / notes
- ✅ Persistence under `%LocalAppData%\SecretBase`
- ⏭ See [v0.2 roadmap](../guides/v0.2-roadmap.md)

## Security spine

```
UI / future AI
    ↓
Command (CreativeCommand / AiCommand / MusicCommand …)
    ↓
Service (validated ids / queries only)
    ↓
Platform (ITargetLaunchService / ICursorLaunchService / browser)
```

Web Widget: WebView2 with **no Host Bridge**. Dangerous URL schemes rejected.

## Related docs

- [Widget architecture](widget-architecture.md)
- [Desktop overlay](desktop-overlay.md)
- [Security boundaries](security-boundaries.md)
- [Creative Workspace](creative-workspace.md)
- [AI Workspace](ai-workspace.md)
- [Keyboard shortcuts](../guides/keyboard-shortcuts.md)
