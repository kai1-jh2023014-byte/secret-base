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
| **Widgets** | Views (`Clock`, `Text`, `Web`, `Calendar`, `Music`), theme helpers | Persistence paths, Desktop chrome, Overlay |
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

- Sources hub UI (`MusicWidgetView`) — open Spotify/YouTube/Web in Untrusted WebView2
- Minimal `IMusicProvider` (`OpenInWidget` now; API providers later)
- Distinct from Web Widget — see [music-widget.md](music-widget.md)

## Persistence

- First-run seeds Clock + Text only (Web/Calendar/Music via FABs)
- `schemaVersion` **2** (Blocks); widget types additive

## Security

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text | None | Local-only |
| Calendar | Optional HTTPS ICS + OAuth read | User ICS URL and/or AppData OAuth client; tokens in Credential Manager |
| Web | WebView2 | Untrusted; no host bridge |
| Music | WebView2 when browsing | Untrusted; same harden as Web; sources metadata only |

Overlay hit-test / DWM are independent of widget types (PR #8).
