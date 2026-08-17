# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. Clock was the first
**reference implementation**; Text, Web, Calendar, and Music reuse the same host patterns.

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, configs, Calendar + Music models/providers (pure), `ThemeDefinition`, `ITimeProvider` | XAML, WinUI/WebView2, network I/O |
| **Infrastructure** | JSON persistence, Google ICS + Google API OAuth read providers, agenda cache | Widget visuals, Overlay HWND |
| **Platform.Abstractions** | `ISecureSecretStore`, overlay/launch contracts | Implementations |
| **Widgets** | Views (`Clock`, `Text`, `Web`, `Calendar`, `Music`, `Creative`, `Ai`, `Apps`, `Assistant`), theme helpers | Persistence paths, Desktop chrome, Overlay |
| **App** | Host, `WidgetFrame`, type→view wiring, FABs | Domain math beyond hosting |

## Shared

- Same `WidgetInstance` + JSON layout persistence + `WidgetFrame` move/resize
- Same `ThemeDefinition` tokens
- Unknown types skipped with a warning

## Calendar-specific

- Today agenda UI (`CalendarWidgetView`) — Refresh / Open / Connect (OAuth)
- Hub via `ICalendarProvider` + `Capabilities` (Local, Mock, Google ICS, Google API read)
- Notion Calendar / TimeTree: **not** providers — see [calendar-integration.md](calendar-integration.md)
- See [calendar-widget.md](calendar-widget.md)

## Music-specific

- Native Music UI (`MusicWidgetView`) — search / current track / transport
- `MusicCommand` → `MusicCommandService` → `IMusicProvider` (Demo catalog today)
- Optional browser open for registered web sources — see [music-widget.md](music-widget.md) / [music-commands.md](music-commands.md)

## Creative-specific

- Desk UI (`CreativeWorkspaceView`) — favorites / recent / search registered items
- `CreativeCommand` → `CreativeCommandService` → workspace store → `ITargetLaunchService`
- See [creative-workspace.md](creative-workspace.md)

## AI Workspace vs Secret Base AI

- `WidgetTypes.Ai` — launcher hub (Cursor desktop + official websites). See [ai-workspace.md](ai-workspace.md).
- `WidgetTypes.Assistant` — in-app chat that selects registered tools only. See [ai-assistant.md](ai-assistant.md).

Do not merge these widgets. Humans and the assistant share the same Command services.

## Persistence

- First-run seeds Clock + Text only (more via **Add Widget** catalog / shortcuts)
- `schemaVersion` **2** (Blocks); widget types additive; Creative items in `creative/workspace.json`; Projects in `creative/projects.json`; My Apps in `apps/apps.json`
- WidgetFrame: move / resize / **remove (×)**; WebView2 disposed on unload/re-render
- Classroom catalog item is a **Web preset**, not a new widget type

## Security

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text | None | Local-only |
| Calendar | Optional HTTPS ICS + OAuth read | User ICS URL and/or AppData OAuth client; tokens in Credential Manager |
| Web | WebView2 | Untrusted; no host bridge. Classroom catalog uses this. |
| Music | Commands + optional browser | Native UI; Demo catalog; no Host Bridge; APIs deferred |
| Creative | Open registered paths only | Favorites/Recent/Dashboard; no Explorer; CreativeCommand boundary |
| AI Workspace | Cursor + official AI websites | Project → Cursor; no in-app LLM; AiCommand boundary |
| Secret Base AI | OpenAI HTTP + registered tools | Tools → existing Commands; keys in Credential Manager; no Host Bridge |
| Apps | Launch registered targets only | AppCommand; no Shell/args/admin; missing files reported by host |

Overlay hit-test / DWM are independent of widget types (PR #8).
