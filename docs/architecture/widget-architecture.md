# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. The Clock Widget was the first **reference implementation**; Text Widget is the second built-in type and reuses the same host patterns.

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, position/size, per-type configuration (`ClockWidgetConfiguration`, `TextWidgetConfiguration`), `ClockDisplayFormatter`, `ThemeDefinition`, `ITimeProvider` | XAML, timers, Win2D/WinUI types |
| **Infrastructure** | JSON layout/theme persistence (`JsonLayoutStore`, `JsonThemeStore`) | Widget visuals |
| **Widgets** | Widget views (`ClockWidgetView`, `TextWidgetView`), theme brush mapping helpers | OS APIs, persistence paths, Desktop chrome |
| **App** | Desktop canvas host, drag/resize chrome (`WidgetFrame`), composition root, type→view wiring | Clock formatting / Text edit rules beyond hosting |

## WidgetInstance

```
WidgetInstance
├── Id (Guid)
├── Type (e.g. "clock", "text")
├── Position (X/Y)
├── Size (Width/Height)
├── RoomId (future multi-room)
└── Configuration (JSON dictionary)
```

`DesktopLayout` holds `List<WidgetInstance>` plus `schemaVersion` for migrations.

## Shared vs type-specific

### Shared (Clock + Text)

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

## Core ↔ UI boundary

- Core owns configuration + pure formatting (Clock). Text content is plain data in Core.
- Views only render and handle local interaction.
- Host chrome (move/resize) lives in App (`WidgetFrame`) so every widget reuses the same placement behavior.

## Persistence

- Layout: `%LocalAppData%\SecretBase\layouts\{roomId}.layout.json`
- Theme: `%LocalAppData%\SecretBase\themes\{themeId}.theme.json`
- Move/resize, Text edits, and Exit all save layout. Atomic write via `.tmp` then replace.
- `schemaVersion` remains **1**. Existing Clock-only layout files still load; Desktop may seed a missing Text widget until Add Widget UI exists.
- First-run `DesktopLayout.CreateDefault()` seeds Clock + Text.

## Security (built-in widgets)

Clock and Text are local-only:

- No network
- No file access from the widget itself
- No process/OS modification

File IO is confined to Infrastructure persistence of layout/theme JSON.
