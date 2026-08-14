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
| **Widgets** | Views (`Clock`, `Text`, `Web`, `Calendar`, `Music`, `Creative`), theme helpers | Persistence paths, Desktop chrome, Overlay |

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

## Persistence

- First-run seeds Clock + Text only (Web/Calendar/Music/Creative via FABs)
- `schemaVersion` **2** (Blocks); widget types additive; Creative items in `creative/workspace.json`; Projects in `creative/projects.json`

## Security

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text | None | Local-only |
| Calendar | Optional HTTPS ICS + OAuth read | User ICS URL and/or AppData OAuth client; tokens in Credential Manager |
| Web | WebView2 | Untrusted; no host bridge |
| Music | Commands + optional browser | Native UI; Demo catalog; no Host Bridge; APIs deferred |
| Creative | Open registered paths only | Favorites/Recent; no Explorer; CreativeCommand boundary |

Overlay hit-test / DWM are independent of widget types (PR #8).
