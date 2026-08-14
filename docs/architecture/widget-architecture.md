# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. Clock was the first
**reference implementation**; Text, Web, and Calendar reuse the same host patterns.

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, configs, `CalendarEvent` + `ICalendarProvider` + `CalendarService` + ICS parse (pure), `ThemeDefinition`, `ITimeProvider` | XAML, WinUI/WebView2, network I/O |
| **Infrastructure** | JSON persistence, Google ICS + Google API OAuth read providers, agenda cache | Widget visuals, Overlay HWND |
| **Platform.Abstractions** | `ISecureSecretStore`, overlay/launch contracts | Implementations |
| **Widgets** | Views (`Clock`, `Text`, `Web`, `Calendar` Today agenda), theme helpers | Persistence paths, Desktop chrome, Overlay |

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

## Persistence

- First-run seeds Clock + Text only (Web/Calendar via FABs)
- `schemaVersion` **2** (Blocks); widget types additive

## Security

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text | None | Local-only |
| Calendar | Optional HTTPS ICS + OAuth read | User ICS URL and/or AppData OAuth client; tokens in Credential Manager |
| Web | WebView2 | Untrusted; no host bridge |

Overlay hit-test / DWM are independent of widget types (PR #8).
