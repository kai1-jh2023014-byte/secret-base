# AGENTS.md — Secret Base handoff for AI / developers

> **Single Source of Truth:** Git history, source code, README, and `docs/`.
> Do **not** invent prior conversation decisions. If docs and code disagree, prefer **code + commit history**, then update docs.

This file is the entry point for continuing development. Detailed design lives in linked docs — avoid duplicating them here.

---

## Project Overview

Secret Base is a **Personal Desktop Environment**:

> Make your PC feel like *your* secret base.

**Windows:** overlay-style WinUI 3 app — not an Explorer/Taskbar replacement.
**macOS:** Avalonia **workspace window** — not a Finder/Dock/menu-bar replacement.

MVP focus: Desktop host + widgets (Clock is the reference implementation). Full product vision (Rooms, plugins, marketplace) is out of scope until requested.

---

## Architecture

Layered solution (see [docs/architecture/overview.md](docs/architecture/overview.md)):

| Project | Role |
|---------|------|
| `SecretBase.App` | WinUI shell, Desktop host, `WidgetFrame` (Windows) |
| `SecretBase.App.Mac` | Avalonia workspace host (macOS) |
| `SecretBase.Core` | Domain models. **No OS UI/API** |
| `SecretBase.Infrastructure` | Logging, AppData paths, JSON stores |
| `SecretBase.Platform.Abstractions` | OS contracts |
| `SecretBase.Platform.Windows` | Windows adapters only |
| `SecretBase.Platform.Mac` | macOS adapters only |
| `SecretBase.Widgets` | Built-in WinUI widget views |

```text
App (WinUI) or App.Mac (Avalonia)
 ├── Widgets (WinUI views; Mac has its own chrome)
 ├── Core (models / formatters)
 ├── Infrastructure (JSON / logs / paths)
 └── Platform.Abstractions ← Platform.Windows | Platform.Mac
```

macOS details: [docs/architecture/macos.md](docs/architecture/macos.md)

---

## Tech Stack

**As pinned in repo (verify in files, do not guess):**

| Piece | Version / choice | Source |
|-------|------------------|--------|
| .NET SDK | `10.0.302` (`rollForward: latestFeature`) | `global.json` |
| TFM (Core/Infra/Platform.Abstractions/Platform.Mac/App.Mac) | `net10.0` | `*.csproj` |
| TFM (App / Widgets / Platform.Windows) | `net10.0-windows10.0.26100.0` | `*.csproj` |
| Windows App SDK | `2.3.1` | `Directory.Build.props` |
| Windows UI | WinUI 3 (WASDK) | App / Widgets |
| macOS UI | Avalonia 11.3.14 | App.Mac |
| Packaging (Windows) | Unpackaged + WASDK self-contained | App `.csproj` |
| Default Windows platform | **x64** | App/Widgets + scripts |
| Tests | xUnit | `tests/` |

---

## Development Commands

Windows host with .NET 10 SDK:

```powershell
.\run.ps1
.\build.ps1
.\test.ps1
```

macOS:

```bash
./run-mac.sh
```

Exit: **Ctrl+Shift+Q** or close the window → process end only; OS shell untouched.

Local data:

- Windows: `%LocalAppData%\SecretBase\`
- macOS: `~/Library/Application Support/SecretBase\`

---

## Security Principles

See [docs/architecture/security-boundaries.md](docs/architecture/security-boundaries.md).

1. Do not implement destructive OS actions (delete/move files except Block intake of Desktop shortcuts, kill processes, registry/shell edits, elevation).
2. Do not inject JS bridges from WebView into host APIs.
3. Do not give AI unrestricted OS control.
4. Plugins must never access Core internals.
5. Logging must avoid secrets / unnecessary PII.

---

## OS shell principles

- Overlay / workspace process only — never replace Explorer, Taskbar, Finder, Dock, or the menu bar.
- All OS-facing code in `SecretBase.Platform.Windows` or `SecretBase.Platform.Mac`.
- Safe exit = process terminate only.

**Forbidden:** Explorer/Dock injection, undocumented shell replacement, patching system files.

---

## Persistence

Same JSON schema on both OS. Writes: `.tmp` → copy over target → delete `.tmp`.

---

## Testing

```powershell
.\test.ps1
```

Prefer Core / Infrastructure / Platform.Mac unit tests for new domain logic. WinUI and Apple notarization cannot run on Linux CI.

---

## Current status (v0.6 Base Experience)

Working: overlay desktop, Clock (Base status style), Calendar, Music (IMusicProvider + Spotify OAuth), Creative Projects, My Apps allowlist, Workspace block, Base AI (context → plan → confirm → action, remote→local fallback), local Todo/Focus, time-aware workspace suggestions, file cleanup suggestions, first-run onboarding that does not wipe existing users.

Not built: Rooms UI, plugins, marketplace, YouTube Music official API (open-web only), Google Tasks write, unrestricted filesystem/shell.

---

## Git Rules

- Prefer small, descriptive commits on feature branches.
- Do **not** force-push, reset shared history, or delete remote branches unless explicitly requested.
- Do **not** change code based on guessed “past conversation” decisions — cite commits/docs/code.

---

## Forbidden Changes

Unless the human explicitly requests otherwise:

1. Do not modify Explorer, Taskbar, Finder, Dock, or OS shell internals.
2. Do not add Win32/WinUI/WASDK/AppKit calls into `SecretBase.Core`.
3. Do not build a large Plugin framework or speculative factories “for later.”
4. Do not trust WebView content or plugins with Core access.
5. Do not force-push / rewrite `main` history.
6. Do not rewrite the Windows WinUI host into Avalonia.

---

## Doc index

- [README.md](README.md)
- [Architecture overview](docs/architecture/overview.md)
- [macOS host](docs/architecture/macos.md)
- [Tech stack](docs/architecture/tech-stack.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Decision log](docs/decisions/README.md)
