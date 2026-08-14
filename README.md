# Secret Base

> Make your PC feel like *your* secret base — a Personal Creative Desktop Environment for Windows.

**v0.1** is the first daily-usable release: a transparent desktop overlay where your Projects, AI tools, Calendar, Music, and notes live together — without replacing Explorer or the Taskbar.

## What you can do

| Area | Experience |
|------|------------|
| **Overlay** | Wallpaper shows through; click outside widgets → normal Desktop |
| **Widgets** | Clock, Text, Web, Calendar, Music, AI, Creative (Projects) |
| **Projects** | Create a Project → Dashboard → Open Folder / Cursor / ChatGPT |
| **AI** | Launch Cursor or official ChatGPT / Claude / Gemini websites |
| **Theme** | Shared colors, fonts, corner radius, transparency |
| **Safe Exit** | **Ctrl+Shift+Q** — save layout and leave Windows untouched |

## Quick start

```powershell
.\build.ps1
.\test.ps1
.\run.ps1
```

| Shortcut | Action |
|----------|--------|
| **Ctrl+Shift+N** | Add Widget (catalog) |
| **Ctrl+Shift+B** | Add Block |
| **Ctrl+Shift+W** | Add Web |
| **Ctrl+Shift+C** | Add Calendar |
| **Ctrl+Shift+M** | Add Music |
| **Ctrl+Shift+E** | Add Creative (Projects) |
| **Ctrl+Shift+A** | Add AI Workspace |
| **Ctrl+Shift+T** | Theme |
| **Ctrl+Shift+D** | Debug chrome |
| **Ctrl+Shift+Q** | Safe Exit |

Full list: [docs/guides/keyboard-shortcuts.md](docs/guides/keyboard-shortcuts.md)

## Principles

1. **Do not break Windows** — overlay app; no Explorer/Taskbar surgery.
2. **Security first** — least privilege; dangerous OS actions stay out of scope.
3. **AI is never unrestricted** — Command → Service → validated Platform launch.
4. **Plugins are untrusted** — Core is not a plugin playground.
5. **Core ⊥ Platform** — Windows APIs stay in `SecretBase.Platform.Windows`.

## Architecture (short)

```
App (DesktopPage + Overlay)
  → Widgets (UI)
  → Core (models, Commands, validation)
  → Infrastructure (JSON AppData)
  → Platform.Windows (launch, Cursor, overlay HWND)
```

Details: [docs/architecture/overview.md](docs/architecture/overview.md)

## Local data

```
%LocalAppData%\SecretBase\
  creative\     # workspace.json, projects.json
  layouts\
  themes\
  logs\
  …
```

## Tech stack

| Piece | Choice |
|-------|--------|
| Language | C# |
| Runtime | **.NET 10 LTS** |
| UI | **WinUI 3** |
| Platform | **Windows App SDK 2.3.1** |
| Web | **WebView2** (Untrusted; no Host Bridge) |

## Docs

- [Overview](docs/architecture/overview.md)
- [Widget architecture](docs/architecture/widget-architecture.md)
- [Desktop overlay](docs/architecture/desktop-overlay.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Creative Workspace / Projects](docs/architecture/creative-workspace.md)
- [AI Workspace](docs/architecture/ai-workspace.md)
- [v0.2 Roadmap](docs/guides/v0.2-roadmap.md)
- [Decision log](docs/decisions/README.md)

## v0.1 intentionally does **not** include

ChatGPT/Claude/Gemini/Cursor APIs, AI agents, MCP, Spotify/YouTube APIs, TimeTree/Notion Calendar APIs, Taskbar/Explorer/Shell hacks, auto-install, or admin elevation.
