# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. Clock was the first
**reference implementation**; Text and Web reuse the same host patterns.

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, position/size, per-type configuration (`ClockWidgetConfiguration`, `TextWidgetConfiguration`, `WebWidgetConfiguration` + `WebUrlValidator`), `ClockDisplayFormatter`, `ThemeDefinition`, `ITimeProvider` | XAML, timers, Win2D/WinUI/WebView2 types |
| **Infrastructure** | JSON layout/theme persistence (`JsonLayoutStore`, `JsonThemeStore`) | Widget visuals |
| **Widgets** | Widget views (`ClockWidgetView`, `TextWidgetView`, `WebWidgetView`), theme brush mapping helpers | OS APIs (beyond WebView2), persistence paths, Desktop chrome |
| **App** | Desktop canvas host, drag/resize chrome (`WidgetFrame`), composition root, type→view wiring | Clock formatting / Text edit / URL validation rules beyond hosting |

## WidgetInstance

```
WidgetInstance
├── Id (Guid)
├── Type (e.g. "clock", "text", "web")
├── Position (X/Y)
├── Size (Width/Height)
├── RoomId (future multi-room)
└── Configuration (JSON dictionary)
```

`DesktopLayout` holds `List<WidgetInstance>` plus `schemaVersion` for migrations
(current schema **2** adds `Blocks`; widget types remain additive inside `widgets[]`).

## Shared vs type-specific

### Shared (Clock + Text + Web)

- Same `WidgetInstance` + JSON layout persistence
- Same `WidgetFrame` for move / resize / min size (drag handle + corner grip only)
- Same `ThemeDefinition` tokens for surface colors, font family, corner radius, transparency
- Unknown `Type` values are skipped with a warning (not fatal)

### Clock-specific

- Core: `ClockWidgetConfiguration`, `ClockDisplayFormatter`, `ITimeProvider`
- View: `ClockWidgetView` (timer lifecycle; local-only display)

### Text-specific

- Core: `TextWidgetConfiguration` (`Text`, `FontSize`, `TextAlignment`)
- View: `TextWidgetView` — display mode → double-click → edit (`TextBox`) → commit/cancel
  - Commit: focus loss or **Ctrl+Enter** (Enter alone inserts a newline for notes)
  - Cancel: **Escape**
- Editing lives in the content area; drag stays on `WidgetFrame`'s drag bar so edit gestures do not move the widget

### Web-specific

- Core: `WebWidgetConfiguration` (`Url`), `WebUrlValidator` (http/https only)
- View: `WebWidgetView` — WebView2 + minimal URL/Go/Reload toolbar
- Content is **Untrusted**; no host bridge (see [web-widget.md](web-widget.md), [security-boundaries.md](security-boundaries.md))
- Drag stays on `WidgetFrame` DragBar so WebView pointer capture does not move the widget

## Core ↔ UI boundary

- Core owns configuration + pure formatting (Clock) + URL validation (Web). Text/Web content strings are plain data in Core.
- Views only render and handle local interaction (WebView2 stays in Widgets).
- Host chrome (move/resize) lives in App (`WidgetFrame`) so every widget reuses the same placement behavior.

## Persistence

- Layout: `%LocalAppData%\SecretBase\layouts\{roomId}.layout.json`
- Theme: `%LocalAppData%\SecretBase\themes\{themeId}.theme.json`
- Move/resize, Text edits, Web URL commits, and Exit all save layout. Atomic write via `.tmp` then replace.
- `schemaVersion` is **2** (Blocks). Existing Clock/Text layouts still load; Web instances are additive.
- First-run `DesktopLayout.CreateDefault()` seeds Clock + Text only (Web is added via **Web** FAB).

## Security (built-in widgets)

| Widget | Network | Notes |
|--------|---------|-------|
| Clock / Text | None | Local-only; no file/process access from the widget itself |
| Web | Yes (WebView2) | Untrusted document; scheme gate + no host bridge |

File IO is confined to Infrastructure persistence of layout/theme JSON.
