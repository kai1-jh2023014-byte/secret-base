# Theme editor

Clock, Text (boxes), and Blocks share one room `ThemeDefinition`.

## How to open

- Always-visible **Aa** button (bottom-left, next to **+**)
- Debug chrome **Theme** (Ctrl+Shift+D)
- **Ctrl+Shift+T**

## What you can change

| Control | Affects |
|---------|---------|
| Preset | Quick looks: Default, Midnight, Warm Paper, Forest, Ocean, Soft Rose |
| Widget / Block background | Clock, Text, Block surfaces |
| Clock / Text / title color | Time, text body, Block titles |
| Date / muted text | Clock date, hints |
| Accent | **+** FAB and accents |
| Font | Clock, Text, Block labels |
| Corner radius | Rounded corners |
| Surface opacity | Widget/Block transparency |

Live preview in the dialog. **Apply** writes `%LocalAppData%\SecretBase\themes\default.theme.json` and re-renders Desktop.

## Layers

- **Core:** `ThemeDefinition`, `ThemePresets`
- **Infrastructure:** `JsonThemeStore` (unchanged schema)
- **App:** Theme dialog on `DesktopPage`
- **Widgets / BlockFrame:** existing `ApplyTheme` paths

No per-widget theme registry yet — one room theme keeps V1 simple.
