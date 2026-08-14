# Theme editor

All desktop widgets and Blocks share one room `ThemeDefinition`.

## How to open

- Always-visible **Aa** FAB
- Debug chrome **Theme** (Ctrl+Shift+D)
- **Ctrl+Shift+T**

## What you can change

| Control | Affects |
|---------|---------|
| Preset | Default, Midnight, Warm Paper, Forest, Ocean, Soft Rose |
| Widget / Block background | Surfaces for Clock, Text, Web, Calendar, Music, Creative, AI, Blocks |
| Foreground / muted | Titles, body, hints |
| Accent | Primary FAB (+) and accents |
| Font | Shared font family |
| Corner radius | Rounded corners |
| Surface opacity | Widget/Block transparency |

Live preview in the dialog. **Apply** writes `%LocalAppData%\SecretBase\themes\default.theme.json` and re-renders Desktop.

## Layers

- **Core:** `ThemeDefinition`, `ThemePresets`
- **Infrastructure:** `JsonThemeStore`
- **App:** Theme dialog on `DesktopPage`
- **Widgets / BlockFrame / WidgetFrame:** `ApplyTheme` paths

One room theme keeps v0.1 simple — no per-widget theme registry.
