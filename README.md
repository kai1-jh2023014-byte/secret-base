# Secret Base

**Your computer should know what matters to you — and still wait before it acts.**

Secret Base is a Personal AI OS: a quiet overlay on Windows, a workspace window on macOS. It observes enough to understand *now* and *next*, remembers what you allow, prepares a workspace, and asks before it opens anything.

There are no product screenshots in this repository yet. Capture list: [docs/design/screenshots.md](docs/design/screenshots.md).

## What is Secret Base?

Secret Base sits on your desktop without replacing Explorer, Finder, Dock, or the taskbar.

It is not a chatbot with widgets around it. **Base AI** is the intelligence layer across calendar, projects, todos, memory, activity, and named integrations. Chat is one entrance. Command Center (Ctrl+Space), Quick Capture, the Clock, and the Base card are the others.

v1.1 adds a **Universal Integration Platform**: homemade apps and HTTP APIs connect through a manifest. Secret Base does not take a dependency on each vendor SDK.

## Why Secret Base?

Most “AI desktops” either take over the OS or dump a chat pane on the wallpaper. Secret Base does neither.

- The shell stays yours. If Secret Base exits, Windows and macOS are unchanged.
- Intelligence is local-first: briefing, search, focus, continue, and capture work without an LLM.
- Remote AI (OpenAI / Gemini) is optional. Missing keys fall back to Local, then to a calm unavailable state.
- Launches, file opens, and workspace continue still confirm. Learning never turns confirmation into auto-launch.

## Core Philosophy

```
Observe → Understand → Remember → Predict → Prepare → Suggest → Confirm → Act → Learn
```

| Step | What Secret Base actually does |
|------|-------------------------------|
| Observe | Windows foreground/idle (sanitized). macOS observation is explicitly unsupported. |
| Understand | Situation + User State + Intent with evidence and confidence |
| Remember | Ranked memory you can search and forget |
| Predict | Daily briefing, continuation, next likely action |
| Prepare | Workspace session — **no apps launch** |
| Suggest | Quiet automation and Attention Center. Silent in focus and quiet hours |
| Confirm | Continue dialog, Base AI Run/Cancel, integration writes |
| Act | Registered tools and allowlisted launches only |
| Learn | Ranking and feedback. **Never** privilege escalation |

## Features

### Personal Space

The overlay is a room, not a widget tray. Default first-run layout is **Clock + Text**. Clock Base style shows **Now** and **Next**. The Base card is situation (greeting, continuation, today, next), not a metric dump.

Appearance is yours: theme, accent, density, shape, motion, transparency. Saved as an explicit style profile — never inferred personality.

### Base AI

Understands the space, then suggests, then asks. Opening copy is context, not an empty chat. Tools go through existing Commands. No Host Bridge. API keys never appear in the transcript.

### Memory

What Secret Base remembers: source, importance, expiry, Forget. Secrets and empty captures are refused.

### Activity

Meaningful timeline after dedup — not a keystroke log. No clipboard, passwords, mic, camera, or page contents.

### Intent

Evidence-based. “Why this suggestion?” is a first-class Command Center action.

### Workspace

Prepare quietly. Continue still confirms before opening a registered project or app.

### Automation

Persisted rules, cooldown, intervention modes. Quiet by default. You enable a rule; Secret Base does not surprise you.

### Command Center

**Ctrl+Space.** Ranked continue, briefing, focus, capture, memory, timeline, apps, projects, tasks. ↑↓ Enter Esc. LLM last.

### Quick Capture

“What do you want to remember?” → Auto / Idea / Todo / Note / Memory / Project → Save. Short path. Secrets refused.

### Search

Local projects, apps, calendar, tasks, memory, integration metadata. No disk crawl.

### Calendar

Local calendar + upcoming events on Clock and briefing. Official Google Calendar stays a Web Widget if you want it — Secret Base does not fake a Calendar API.

### Focus

Safe Auto timer. No apps launch. Interruptions stay off unless you allow them.

### Integration Platform

Manifest → registry → named capability → permission → Safety Gate → confirm if required → HTTP(S) or loopback, or `app.open` of a **registered My Apps** target.

Not in v1.1: MCP servers, OAuth UI, inbound webhook HTTP listeners, WebSocket/gRPC transports. Those names may appear in schemas as reserved — they are not a working path.

### Privacy & Safety

Privacy Center lists observed / remembered / allowed / confirmation / never collected. Quiet hours. Automation permission. Safe Exit is **Ctrl+Shift+Q** (process end only).

## Architecture

```
Windows / macOS
       ↓
Observation          (Windows: sanitized foreground/idle)
       ↓
Activity
       ↓
Memory + Situation
       ↓
User State
       ↓
Intent
       ↓
Workspace / Session  (prepare = Safe Auto)
       ↓
Automation
       ↓
Intervention
       ↓
Safety Gate
       ↓
Suggestion / Confirmation
       ↓
Action               (registered tools / allowlisted launch)
       ↓
Learning             (ranking only — never auto-launch)
```

Layers:

```
App (WinUI overlay)  or  App.Mac (Avalonia workspace)
  → Widgets (WinUI) / Mac window chrome
  → Core (models, Commands, validation — no OS APIs)
  → Infrastructure (JSON AppData)
  → Platform.Windows | Platform.Mac
```

Integration path:

```
External App / API
        ↓
    Connector (manifest)
        ↓
   Named capability
        ↓
   Permission
        ↓
  Safety Gate
        ↓
Suggestion / Confirmation
        ↓
      Action
```

Details: [docs/architecture/overview.md](docs/architecture/overview.md) · [docs/architecture/personal-ai-os.md](docs/architecture/personal-ai-os.md) · [docs/integrations/overview.md](docs/integrations/overview.md)

## Safety

Three lanes, unchanged:

| Lane | Examples |
|------|----------|
| **Safe Auto** | Prepare workspace, focus timer, memory write, capture, briefing |
| **Confirmation** | App launch, file open, calendar write, music play, workspace continue, integration write/execute |
| **Explicit** | Unregister / return a Block item. `files_delete` never calls OS `File.Delete` |

No arbitrary shell, PowerShell, or exe. No always-on LLM. WebView2 has no Host Bridge.

## Privacy

Windows observation: process name + idle via documented APIs. Browser titles dropped unless they name a registered project.

Never collected: keystrokes, clipboard, passwords, browser page bodies, microphone, camera, screenshots, arbitrary document contents.

Connector payloads are untrusted data, never system instructions. Secrets live in Credential Manager / Keychain.

## Integrations

1. Ship a small HTTP API (or events) from your app.
2. Copy [`examples/integrations/secretbase.integration.json`](examples/integrations/secretbase.integration.json).
3. **My Integrations** — paste JSON (Register pasted) or enable the example apps.
4. Grant permissions. Reads can run; writes and `app.open` confirm.
5. Ask Base AI to use a **named** capability. Never arbitrary URLs or HTTP.

Guide: [docs/integrations/overview.md](docs/integrations/overview.md) · security: [docs/integrations/security.md](docs/integrations/security.md)

## Supported Platforms

| Host | What you get |
|------|----------------|
| **Windows** | Chromeless overlay on the wallpaper. Click outside widgets → normal Desktop. |
| **macOS** | Avalonia workspace window. Does **not** replace Finder or Dock. Observation is `Unsupported`. |

## Tech Stack

| Piece | Choice |
|-------|--------|
| Language | C# |
| Runtime | **.NET 10 LTS** |
| UI | **WinUI 3** (Windows) / **Avalonia 11.3.14** (macOS) |
| Platform | **Windows App SDK 2.3.1** / **Platform.Mac** (LaunchAgent, Keychain, `open`) |
| Web | **WebView2** on Windows (Untrusted; no Host Bridge). macOS v1 has no WebView |

## Development

```powershell
.\build.ps1
.\test.ps1
.\run.ps1
```

**macOS:**

```bash
./run-mac.sh
```

| Shortcut | Action |
|----------|--------|
| **Ctrl+Space** | Command Center |
| **Ctrl+Shift+K** | Focus Base AI command bar |
| **Ctrl+Shift+N** | Add Widget |
| **Ctrl+Shift+B** | Add Block |
| **Ctrl+Shift+T** | Appearance |
| **Ctrl+Shift+D** | Debug chrome |
| **Ctrl+Shift+Q** | Safe Exit |

Full list: [docs/guides/keyboard-shortcuts.md](docs/guides/keyboard-shortcuts.md)

### Local data

```
Windows: %LocalAppData%\SecretBase\
macOS:   ~/Library/Application Support/SecretBase\
  apps\         # apps.json (My Apps)
  creative\     # workspace.json, projects.json
  layouts\
  themes\       # default.theme.json (Appearance)
  logs\
  settings\     # assistant, base, todos, memory, activity, sessions, integrations, automation
```

## Testing

Linux CI: Core, Infrastructure, Platform.Mac, App.Mac build.

Windows CI: Core, Infrastructure, Platform.Windows, WinUI App build.

```powershell
dotnet test tests/SecretBase.Core.Tests --configuration Release
dotnet test tests/SecretBase.Infrastructure.Tests --configuration Release
```

## Roadmap

Shipped through v1.1: overlay, widgets, Personal AI OS loop, Command Center, integrations (HTTP manifest).

Not in scope: unrestricted agents, MCP with OS power, Computer Use, arbitrary shell/file tools, Spotify/YouTube official APIs, Google Classroom API (open the official site only), Taskbar/Explorer/Dock surgery, auto-install, admin elevation, inbound webhook servers, OAuth UI.

Design language: [docs/design/language.md](docs/design/language.md). Appearance: [docs/architecture/theme-editor.md](docs/architecture/theme-editor.md).

## Project Philosophy

Secret Base should feel like a place you leave on the machine — quiet, light, not AI-pushy — and like *your* place: theme, density, accent, motion.

```
Secret Base → Personal AI OS → Personal Space → Personal Style
```

## License

No license file is published in this repository yet. Do not assume an OSI license.
