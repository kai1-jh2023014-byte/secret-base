# Secret Base Architecture Overview

Secret Base is an **overlay-style Personal Creative Desktop Environment**. The Windows host is a WinUI 3 overlay. The macOS host is a separate Avalonia workspace window that shares Core.

Highest principle:

> If Secret Base breaks, the OS shell (Explorer / Finder / Dock / Taskbar) must remain intact.

## Layers

| Project | Responsibility |
|---------|----------------|
| `SecretBase.App` | WinUI shell, Desktop host UI, composition root (Windows) |
| `SecretBase.App.Mac` | Avalonia workspace window, composition root (macOS) |
| `SecretBase.Core` | Domain models, Commands, validation. No OS UI/API calls |
| `SecretBase.Infrastructure` | Logging, AppData paths, JSON persistence |
| `SecretBase.Platform.Abstractions` | OS-facing contracts |
| `SecretBase.Platform.Windows` | Overlay HWND, Safe Exit, launch, Cursor discovery |
| `SecretBase.Platform.Mac` | LaunchAgent, Keychain, `/usr/bin/open`, Cursor.app |
| `SecretBase.Widgets` | Built-in WinUI widget views + theme painting |

## Process model (v0.1 / v0.2)

- Unpackaged WinUI 3 app (`WindowsPackageType=None`).
- Closing / Safe Exit ends the process only after layout save.
- No Explorer hooks, no Taskbar replacement, no shell registry mutation.

## v0.1 surface

- ✅ Desktop Overlay (chromeless work-area; widgets-only click-through)
- ✅ Widgets: Clock, Text, Web, Calendar, Music, Creative (Projects + Dashboard), AI Workspace
- ✅ Blocks (launch tiles)
- ✅ Theme + Arrange
- ✅ Projects → Dashboard → Cursor / ChatGPT / resources / notes
- ✅ Persistence under `%LocalAppData%\SecretBase`

## v0.2 surface (this slice)

- ✅ Integration Catalog + `IntegrationCommandService` (router over existing Commands)
- ✅ CalendarCommand / ClassroomCommand / AppCommand (thin; do not rewrite Music/Creative/Ai)
- ✅ My Apps registry (`apps.json`) + My Apps widget
- ✅ Classroom via existing Web Widget preset (`https://classroom.google.com/`) — **no Classroom Widget reimplementation**
- ✅ Grouped Add Widget catalog
- ✅ Secret Base AI (`IAiProvider` + tool registry over existing Commands; OpenAI HTTP MVP)
- ✅ Secret Base AI v0.3 Context Layer (read-only snapshot + suggest vs confirm)
- ✅ Secret Base AI v0.4 Personal AI Workspace (Plan / Confirm / Action / Result; AI MVP complete)
- ⏭ Gemini / Local LLM HTTP, long-term memory, autonomous agents — see [v0.2 roadmap](../guides/v0.2-roadmap.md)

## Security spine

```
UI / Secret Base AI
    ↓
Command (CreativeCommand / AiCommand / MusicCommand / CalendarCommand / ClassroomCommand / AppCommand)
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
- [Secret Base AI](ai-assistant.md)
- [Assistant Context](assistant-context.md)
- [Assistant Tools](assistant-tools.md)
- [Assistant Planning](assistant-planning.md)
- [Assistant Security](assistant-security.md)
- [Assistant Context](assistant-context.md)
- [Assistant Tools](assistant-tools.md)
- [Integration Hub](integration-hub.md)
- [Keyboard shortcuts](../guides/keyboard-shortcuts.md)
- [macOS host](macos.md)
