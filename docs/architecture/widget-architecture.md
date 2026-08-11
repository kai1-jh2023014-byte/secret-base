# Widget Architecture

## Goals

Widgets are the building blocks of the Secret Base Desktop. The Clock Widget is the **reference implementation** for future widgets (Text, App Launcher, Web Content, etc.).

## Responsibilities

| Layer | Owns | Must not own |
|-------|------|--------------|
| **Core** | `WidgetInstance`, `WidgetTypes`, position/size, `ClockWidgetConfiguration`, `ClockDisplayFormatter`, `ThemeDefinition`, `ITimeProvider` | XAML, timers, Win2D/WinUI types |
| **Infrastructure** | JSON layout/theme persistence (`JsonLayoutStore`, `JsonThemeStore`) | Widget visuals |
| **Widgets** | Widget views (`ClockWidgetView`), theme brush mapping helpers | OS APIs, persistence paths, Desktop chrome |
| **App** | Desktop canvas host, drag/resize chrome (`WidgetFrame`), composition root | Clock formatting rules |

## WidgetInstance

```
WidgetInstance
├── Id (Guid)
├── Type (e.g. "clock")
├── Position (X/Y)
├── Size (Width/Height)
├── RoomId (future multi-room)
└── Configuration (JSON dictionary)
```

`DesktopLayout` holds `List<WidgetInstance>` plus `schemaVersion` for migrations.

## Core ↔ UI boundary

- Core produces display strings via `ClockDisplayFormatter` + `ITimeProvider`.
- `ClockWidgetView` only renders and ticks a `DispatcherTimer` (started on Loaded, stopped on Unloaded/Dispose).
- Host chrome (move/resize) lives in App so every future widget reuses the same placement behavior.

## Persistence

- Layout: `%LocalAppData%\SecretBase\layouts\{roomId}.layout.json`
- Theme: `%LocalAppData%\SecretBase\themes\{themeId}.theme.json`
- Move/resize and Exit both save layout. Atomic write via `.tmp` then replace.

## Security (Clock)

Clock is local-only:

- No network
- No file access from the widget itself
- No process/OS modification

File IO is confined to Infrastructure persistence of layout/theme JSON.
