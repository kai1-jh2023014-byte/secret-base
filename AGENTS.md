# AGENTS.md — Secret Base handoff for AI / developers

> **Single Source of Truth:** Git history, source code, README, and `docs/`.  
> Do **not** invent prior conversation decisions. If docs and code disagree, prefer **code + commit history**, then update docs.

This file is the entry point for continuing development. Detailed design lives in linked docs — avoid duplicating them here.

---

## Project Overview

Secret Base is a **Personal Desktop Environment** for Windows:

> Make your Windows PC feel like *your* secret base.

It is an **overlay-style WinUI 3 app**, not an Explorer/Taskbar replacement.  
MVP focus: Desktop host + widgets (Clock is the reference implementation).

Full product vision (Rooms, AI, Local LLM, Plugins, Marketplace, etc.) is intentional but **out of scope until requested**. Ship one widget carefully; abstract only when proven needed.

---

## Architecture

Layered solution (see [docs/architecture/overview.md](docs/architecture/overview.md)):

| Project | Role |
|---------|------|
| `SecretBase.App` | WinUI shell, Desktop host, `WidgetFrame` (drag/resize), composition root |
| `SecretBase.Core` | Domain models (widgets, layout, theme, security enums). **No Windows UI/API** |
| `SecretBase.Infrastructure` | Logging, AppData paths, JSON layout/theme stores |
| `SecretBase.Platform.Abstractions` | `ICompatibilityService`, `ISafeExitService` |
| `SecretBase.Platform.Windows` | Windows adapter implementations only |
| `SecretBase.Widgets` | Built-in widget views (e.g. `ClockWidgetView`), `ThemePainter` |

```text
App (WinUI host)
 ├── Widgets (views)
 ├── Core (models / formatters)
 ├── Infrastructure (JSON / logs / paths)
 └── Platform.Abstractions ← Platform.Windows
```

---

## Tech Stack

**As pinned in repo (verify in files, do not guess):**

| Piece | Version / choice | Source |
|-------|------------------|--------|
| .NET SDK | `10.0.302` (`rollForward: latestFeature`) | `global.json` |
| TFM (Core/Infra/Platform) | `net10.0` | `*.csproj` / `Directory.Build.props` |
| TFM (App / Widgets) | `net10.0-windows10.0.26100.0` | `*.csproj` |
| Windows App SDK | `2.3.1` | `Directory.Build.props`, App/Widgets PackageReference |
| UI | WinUI 3 (via WASDK) | App / Widgets |
| Packaging | Unpackaged (`WindowsPackageType=None`) + WASDK self-contained | App `.csproj` |
| Default platform | **x64** | App/Widgets + scripts |
| Tests | xUnit | `tests/` |

Details: [docs/architecture/tech-stack.md](docs/architecture/tech-stack.md)

---

## Development Commands

From repo root (Windows host with .NET 10 SDK):

```powershell
.\run.ps1      # Debug | x64 — build & run SecretBase.App
.\build.ps1    # Debug | x64 — build solution
.\test.ps1     # Debug — run unit tests
```

Rider: shared Run/Debug config **Secret Base** in `.run/` (see [docs/decisions/2026-08-11-rider-run-configuration.md](docs/decisions/2026-08-11-rider-run-configuration.md)).

Exit: **Exit to Windows Desktop** or close the window → process end only; Windows shell untouched.

Local data: `%LocalAppData%\SecretBase\` (`logs/`, `layouts/`, `themes/`, `settings/`).

---

## Security Principles

See [docs/architecture/security-boundaries.md](docs/architecture/security-boundaries.md).

Hard rules for current work:

1. Do not implement destructive OS actions (delete/move files, kill processes, registry shell edits, elevation).
2. Do not inject JS bridges from WebView2 into host APIs (when Web Content lands).
3. Do not give AI unrestricted OS control (privilege ladder is modeled only: `ActionPrivilege`).
4. Plugins must never access Core internals (future Plugin API only).
5. Logging must avoid secrets / unnecessary PII.

Trust zones (enums in Core): TrustedHost → BuiltInWidget → WebContent / Plugin / AiAgent (latter three untrusted or restricted).

---

## Windows Update Principles

See [docs/architecture/windows-update-resilience.md](docs/architecture/windows-update-resilience.md).

- Overlay process only — never replace Explorer / Taskbar / shell.
- All OS-facing code in `SecretBase.Platform.Windows`.
- Prefer public WASDK / WinUI / managed .NET APIs.
- Safe exit = process terminate only (`ISafeExitService`).

**Forbidden:** Explorer injection/subclassing, undocumented Taskbar shell replacement COM, patching system files, replacing the Windows shell.

---

## Widget Architecture

See [docs/architecture/widget-architecture.md](docs/architecture/widget-architecture.md).

Reference pattern (Clock):

1. **Core** — `WidgetInstance` + type-specific config (`ClockWidgetConfiguration`) + pure formatter (`ClockDisplayFormatter`) + `ITimeProvider`
2. **Infrastructure** — persist `DesktopLayout` / `ThemeDefinition` as JSON
3. **Widgets** — WinUI view + timer lifecycle; theme via `ThemePainter`
4. **App** — place on `Canvas` inside shared `WidgetFrame` (drag + resize); save on change / exit

`WidgetInstance`: `Id`, `Type`, `Position`, `Size`, `RoomId`, `Configuration` (JSON dictionary).  
`WidgetTypes.Clock = "clock"` today. Room UI is not built; always `RoomId.DefaultRoomId` (`"default"`).

---

## Persistence

| Store | Path pattern | Schema |
|-------|--------------|--------|
| Layout | `%LocalAppData%\SecretBase\layouts\{roomId}.layout.json` | `DesktopLayout.SchemaVersion` (currently **1**) |
| Theme | `%LocalAppData%\SecretBase\themes\{themeId}.theme.json` | `ThemeDefinition.SchemaVersion` (currently **1**) |

- JSON: camelCase, indented, trailing commas/comments allowed (`SecretBaseJson`).
- Writes: write `.tmp` → copy over target → delete `.tmp`.
- Migration today: if layout `schemaVersion < 1`, bump to `1`; empty widget list gets a default Clock. **No multi-version migrator yet** — bump schema carefully and add explicit migration when needed.

---

## Theme

`ThemeDefinition` tokens (hex `#AARRGGBB` / `#RRGGBB`): background, foreground, accent, widget surface, font, corner radius, transparency, min size.  
UI mapping: `SecretBase.Widgets.Theming.ThemePainter`. Theme editor UX is **not** implemented yet (tokens persist and apply on load).

---

## Testing

```powershell
.\test.ps1
```

Current coverage (as of Clock milestone): **14** xUnit tests

- Core: identity, RoomId, privilege/trust enums, clock formatting, widget geometry/config round-trip, default layout
- Infrastructure: AppData paths, layout save/load, theme save/load

Prefer Core/Infrastructure unit tests for new domain logic. Do not require UI automation for every change.

---

## Git Rules

- Prefer small, descriptive commits on feature branches.
- Do **not** force-push, reset shared history, rebase onto rewritten history, or delete remote branches unless explicitly requested.
- Do **not** change code based on guessed “past conversation” decisions — cite commits/docs/code.
- Decision log: [docs/decisions/README.md](docs/decisions/README.md)

Known milestones on `main`:

| Commit | Meaning |
|--------|---------|
| `a2f919c` | Foundation: layers, security model, safe exit, docs |
| `d5d0c38` | Dev workflow: `run`/`build`/`test` scripts, Rider `.run/`, x64 default |
| `174e0a1` | Clock widget + persistence + theme + WidgetFrame |

---

## Current MVP

```text
Secret Base Desktop
  ├── Clock Widget     ✅ reference implementation
  ├── Text Widget      ❌ not started
  ├── App Launcher     ❌ not started
  └── WebContent       ❌ not started
```

Roadmap order (README): Text → App Launcher → Drag polish → Theme editor → Web Content → Room switching.

---

## Current Development Status

- **HEAD (expected):** `174e0a10e18cc856f1511810175fcc5d5717b692` — `feat: add clock widget`
- App version constant: `AppInfo.Version = 0.1.0`
- Working: Desktop host, live Clock, drag/resize, layout/theme JSON restore, safe exit, compatibility logging
- Not built: Text/Launcher/WebContent, Room UI, Plugin system, AI/LLM, Taskbar integration, marketplace, advanced PC management

---

## Forbidden Changes

Unless the human explicitly requests otherwise:

1. Do not modify Explorer, Taskbar, or Windows shell internals.
2. Do not add Win32/WinUI/WASDK calls into `SecretBase.Core`.
3. Do not build a large Plugin framework, DI mega-abstraction, event bus, or speculative factories “for later.”
4. Do not trust WebView content or plugins with Core access.
5. Do not force-push / rewrite `main` history.
6. Do not start the next widget (e.g. Text) until instructed after context recovery / planning.

---

## Doc index

- [README.md](README.md)
- [Architecture overview](docs/architecture/overview.md)
- [Tech stack](docs/architecture/tech-stack.md)
- [Widget architecture](docs/architecture/widget-architecture.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Windows Update resilience](docs/architecture/windows-update-resilience.md)
- [Decision log](docs/decisions/README.md)
