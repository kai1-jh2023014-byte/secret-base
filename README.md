# Secret Base

> Make your PC feel like *your* secret base — a Personal Creative Desktop Environment for Windows and macOS.

**v0.1** is the daily-usable overlay. **v0.2–v0.5** add Integration Hub and **Secret Base AI** as a Personal AI Workspace: Context → Plan → Confirmation → Action → Result over existing Commands — still without replacing Explorer or the Taskbar.

## What you can do

| Area | Experience |
|------|------------|
| **Overlay** | Wallpaper shows through; click outside widgets → normal Desktop |
| **Widgets** | Clock (Base status), Text, Web, Calendar, Music, Workspace, AI Workspace, Base AI, Creative (Projects), My Apps |
| **Projects** | Create a Project → Dashboard → Open Folder / Cursor / ChatGPT |
| **Apps** | Register your own exe / folder / https URL and launch it (no Shell, no admin) |
| **Classroom** | Opens official Google Classroom in the **existing Web Widget** (no new Classroom UI) |
| **AI** | **Base AI** understands Calendar, Projects, Todo, Workspace, Music, and Apps; prepares a workspace quietly; confirms before launch. Remote AI falls back to Local. |
| **Theme** | Shared colors, fonts, corner radius, transparency |
| **Safe Exit** | **Ctrl+Shift+Q** — process end only; Windows / macOS shell untouched |

## Quick start

```powershell
.\build.ps1
.\test.ps1
.\install-launchers.ps1   # Start Menu + Desktop shortcuts (no terminal next time)
.\run.ps1 -ExeOnly        # launch SecretBase.App.exe (needed for Start at login)
```

Or for a quick dev loop: `.\run.ps1`

In the overlay, open **Setup (⚙)** → create shortcuts / enable **Start at login**. AI Settings saves OpenAI and Gemini keys to **separate** Credential Manager slots — use **Test Connection** after saving.

**macOS** (workspace window — does not replace Finder or Dock):

```bash
./run-mac.sh
```

Details: [docs/architecture/macos.md](docs/architecture/macos.md) · [windows-autostart.md](docs/architecture/windows-autostart.md)

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

1. **Do not break the OS shell** — overlay on Windows; workspace window on macOS. No Explorer/Taskbar/Dock/Finder surgery.
2. **Security first** — least privilege; dangerous OS actions stay out of scope.
3. **AI is never unrestricted** — Command → Service → validated Platform launch.
4. **Plugins are untrusted** — Core is not a plugin playground.
5. **Core ⊥ Platform** — OS APIs stay in `SecretBase.Platform.Windows` or `SecretBase.Platform.Mac`.

## Architecture (short)

```
App (WinUI overlay)  or  App.Mac (Avalonia workspace)
  → Widgets (WinUI) / Mac window chrome
  → Core (models, Commands, validation)
  → Infrastructure (JSON AppData)
  → Platform.Windows | Platform.Mac
```

Details: [docs/architecture/overview.md](docs/architecture/overview.md)

## Local data

```
Windows: %LocalAppData%\SecretBase\
macOS:   ~/Library/Application Support/SecretBase\
  apps\         # apps.json (My Apps registry)
  creative\     # workspace.json, projects.json
  layouts\
  themes\
  logs\
  settings\     # assistant.json, base.json (onboarding/workspace), todos.json
```

## Base AI in one paragraph

Base AI is Secret Base's quiet intelligence — not a ChatGPT clone. It observes local context (calendar, projects, todos, registered files/apps, music, focus), can **prepare a workspace** without launching anything, and asks for confirmation before opening a project or app. File cleanup is suggestion-only. `files_delete` never calls OS `File.Delete`. Missing API keys fall back to Local AI.

## Provider setup

Remote (OpenAI / Gemini) is optional. With no API key, Base AI uses **Local** (Ollama) when available, otherwise shows a calm unavailable state. Keys stay in Credential Manager / Keychain.

## Tech stack

| Piece | Choice |
|-------|--------|
| Language | C# |
| Runtime | **.NET 10 LTS** |
| UI | **WinUI 3** (Windows) / **Avalonia 11.3.14** (macOS) |
| Platform | **Windows App SDK 2.3.1** / **Platform.Mac** (LaunchAgent, Keychain, `open`) |
| Web | **WebView2** on Windows (Untrusted; no Host Bridge). macOS v1 has no WebView. |

## Docs

- [macOS host](docs/architecture/macos.md)
- [Overview](docs/architecture/overview.md)
- [Widget architecture](docs/architecture/widget-architecture.md)
- [Desktop overlay](docs/architecture/desktop-overlay.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Creative Workspace / Projects](docs/architecture/creative-workspace.md)
- [AI Workspace](docs/architecture/ai-workspace.md)
- [Secret Base AI](docs/architecture/ai-assistant.md)
- [Assistant Context](docs/architecture/assistant-context.md)
- [Assistant Tools](docs/architecture/assistant-tools.md)
- [AI Planning](docs/architecture/ai-planning.md)
- [AI Security](docs/architecture/ai-security.md)
- [Integration Hub (v0.2)](docs/architecture/integration-hub.md)
- [v0.2 Roadmap](docs/guides/v0.2-roadmap.md)
- [Decision log](docs/decisions/README.md)

## v0.1 / v0.2 intentionally do **not** include

Unrestricted AI agents, MCP with OS power, Computer Use, arbitrary shell/file tools, Spotify/YouTube APIs, TimeTree/Notion Calendar APIs, Google Classroom API (open the official site only), Taskbar/Explorer/Shell hacks, auto-install, or admin elevation.
