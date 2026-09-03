# Appearance Center

All desktop widgets, Blocks, Command Center, and the macOS workspace share one room `ThemeDefinition`. Appearance is an explicit UI preference — Secret Base does not infer personality.

## How to open

- Always-visible **Aa** FAB
- Debug chrome **Theme** (Ctrl+Shift+D)
- **Ctrl+Shift+T**
- macOS workspace **Appearance**

## What you can change

| Control | Affects |
|---------|---------|
| Theme | Complete visual systems: Atelier, Light, Dark, Midnight, Soft, Minimal, Glass, High Contrast (plus Focus, Aurora, Mono, Warm Paper, Forest, Ocean, Soft Rose) |
| Accent | Jade, Blue, Purple, Green, Orange, Pink, Red, Cyan, Custom. Status success / warning / error stay independent |
| Density | Compact / Comfortable / Spacious — spacing and type scale |
| Shape | Soft / Balanced / Sharp — shared corner radius |
| Motion | Subtle / Standard / Reduced (duration 0; Clock and Base card skip pulse) |
| Transparency | Widget / Block surface opacity |
| Advanced | Hex surfaces and font, for fine-tuning after a preset |

Live preview in the dialog. **Apply** writes `%LocalAppData%\SecretBase\themes\default.theme.json` (macOS: Application Support) and re-renders Desktop.

`AppearanceProfile.Format()` is the explicit **My Style** line (theme · density · shape · accent · motion). It is never inferred.

## Layers

- **Core:** `ThemeDefinition`, `ThemePresets`, `AppearanceComposer`, `AccentPalette`, `ThemeMigrator` (schema 2)
- **Infrastructure:** `JsonThemeStore` (normalizes on load)
- **App:** Appearance dialog on `DesktopPage` / Mac `MainWindow`
- **Widgets / BlockFrame / WidgetFrame:** `ApplyTheme` paths

One room theme keeps the overlay coherent — no per-widget theme registry.

Glass stores a blur *token* only. Surfaces use opacity and border; Secret Base does not run compositor blur on every widget.
