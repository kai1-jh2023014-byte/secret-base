# Secret Base

> Make your PC feel like *your* secret base — a Personal Creative Desktop Environment for Windows.

**v0.1** is the daily-usable overlay. **v0.2–v0.4** add Integration Hub and **Secret Base AI** as a Personal AI Workspace: Context → Plan → Confirmation → Action → Result over existing Commands — still without replacing Explorer or the Taskbar.

## What you can do

| Area | Experience |
|------|------------|
| **Overlay** | Wallpaper shows through; click outside widgets → normal Desktop |
| **Widgets** | Clock, Text, Web, Calendar, Music, AI Workspace, Secret Base AI, Creative (Projects), My Apps |
| **Projects** | Create a Project → Dashboard → Open Folder / Cursor / ChatGPT |
| **Apps** | Register your own exe / folder / https URL and launch it (no Shell, no admin) |
| **Classroom** | Opens official Google Classroom in the **existing Web Widget** (no new Classroom UI) |
| **AI** | **Secret Base AI** (Personal AI Workspace: context, plan, confirm, action) plus AI Workspace (Cursor / official ChatGPT / Claude / Gemini websites) |
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
  apps\         # apps.json (My Apps registry)
  creative\     # workspace.json, projects.json
  layouts\
  themes\
  logs\
  settings\     # assistant.json (provider/model/maxSteps; API keys in Credential Manager)
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
- [Secret Base AI](docs/architecture/ai-assistant.md)
- [Assistant Context](docs/architecture/assistant-context.md)
- [Assistant Tools](docs/architecture/assistant-tools.md)
- [Assistant Planning](docs/architecture/assistant-planning.md)
- [Assistant Security](docs/architecture/assistant-security.md)
- [Integration Hub (v0.2)](docs/architecture/integration-hub.md)
- [v0.2 Roadmap](docs/guides/v0.2-roadmap.md)
- [Decision log](docs/decisions/README.md)

## v0.1 / v0.2 intentionally do **not** include

Unrestricted AI agents, MCP with OS power, Computer Use, arbitrary shell/file tools, Spotify/YouTube APIs, TimeTree/Notion Calendar APIs, Google Classroom API (open the official site only), Taskbar/Explorer/Shell hacks, auto-install, or admin elevation.
