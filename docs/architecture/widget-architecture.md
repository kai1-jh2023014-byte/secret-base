# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. Clock was the first
**reference implementation**; Text, Web, and Calendar reuse the same host patterns.

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, position/size, per-type configuration (`Clock` / `Text` / `Web` / `Calendar`), `CalendarMonthBuilder` + `ICalendarEventSource`, `ThemeDefinition`, `ITimeProvider` | XAML, timers, Win2D/WinUI/WebView2 types |
| **Infrastructure** | JSON layout/theme persistence (`JsonLayoutStore`, `JsonThemeStore`) | Widget visuals |
| **Widgets** | Widget views (`ClockWidgetView`, `TextWidgetView`, `WebWidgetView`, `CalendarWidgetView`), theme helpers | OS APIs (beyond WebView2), persistence paths, Desktop chrome |
| **App** | Desktop canvas host, drag/resize chrome (`WidgetFrame`), composition root, type→view wiring | Domain formatting / URL / calendar math beyond hosting |

## WidgetInstance

```
WidgetInstance
├── Id (Guid)
├── Type (e.g. "clock", "text", "web", "calendar")
├── Position (X/Y)
├── Size (Width/Height)
├── RoomId (future multi-room)
└── Configuration (JSON dictionary)
```

`DesktopLayout` holds `List<WidgetInstance>` plus `schemaVersion` for migrations
(current schema **2** adds `Blocks`; widget types remain additive inside `widgets[]`).

## Shared vs type-specific

### Shared (Clock + Text + Web + Calendar)

- Same `WidgetInstance` + JSON layout persistence
- Same `WidgetFrame` for move / resize / min size (drag handle + corner grip only)
- Same `ThemeDefinition` tokens for surface colors, font family, corner radius, transparency
- Unknown `Type` values are skipped with a warning (not fatal)

### Clock-specific

- Core: `ClockWidgetConfiguration`, `ClockDisplayFormatter`, `ITimeProvider`
- View: `ClockWidgetView` (timer lifecycle; local-only display)

### Text-specific

- Core: `TextWidgetConfiguration` (`Text`, `FontSize`, `TextAlignment`)
- View: `TextWidgetView` — display ↔ edit; drag stays on `WidgetFrame` DragBar

### Web-specific

- Core: `WebWidgetConfiguration` (`Url`), `WebUrlValidator` (http/https only)
- View: `WebWidgetView` — WebView2 + minimal toolbar; Untrusted document (no host bridge)

### Calendar-specific

- Core: `CalendarWidgetConfiguration`, `CalendarMonthBuilder`, `ICalendarEventSource` (+ local/empty sources)
- View: `CalendarWidgetView` — month grid, prev/next/today
- v0.1 events are local configuration only (see [calendar-widget.md](calendar-widget.md))

## Core ↔ UI boundary

- Core owns configuration + pure formatting (Clock) + URL validation (Web) + month/event math (Calendar).
- Views only render and handle local interaction.
- Host chrome (move/resize) lives in App (`WidgetFrame`).

## Persistence

- Layout: `%LocalAppData%\SecretBase\layouts\{roomId}.layout.json`
- Theme: `%LocalAppData%\SecretBase\themes\{themeId}.theme.json`
- Move/resize, Text edits, Web URL commits, Calendar month/events, and Exit all save layout.
- `schemaVersion` is **2** (Blocks). Web/Calendar instances are additive — no default-layout replacement.
- First-run seeds Clock + Text only (Web/Calendar via FABs).

## Security (built-in widgets)

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text / Calendar | None (v0.1) | Local-only |
| Web | Yes (WebView2) | Untrusted document; scheme gate + no host bridge |

File IO is confined to Infrastructure persistence of layout/theme JSON.
