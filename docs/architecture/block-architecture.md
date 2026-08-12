# Block Architecture

Blocks are small **rooms on the Desktop** — movable/resizable hosts for launchable items (apps, shortcuts, files, folders).

They are **not** Windows folders and do not replace Explorer.

## Layering

| Layer | Owns |
|-------|------|
| **Core** | `Block`, `BlockItem`, `BlockItemType`, `BlockTargetValidator`, `DefaultBlockFactory` |
| **Infrastructure** | Layout JSON (`blocks` array), schema v1→v2 migration |
| **Platform.Abstractions** | `ITargetLaunchService` / request+result DTOs |
| **Platform.Windows** | `ShellTargetLaunchService` (`Process.Start` + `UseShellExecute`) |
| **App** | `BlockFrame` UI, Add Block dialog, drop-target wiring, composition |

Core has no Win32 / WinUI references.

## Layout schema

- **schemaVersion 1** — widgets only (Clock / Text). Still loads.
- **schemaVersion 2** — adds `blocks: []` (default empty). Load upgrades v1 → v2 in memory without dropping widgets.

```json
{
  "roomId": "default",
  "schemaVersion": 2,
  "widgets": [ /* unchanged */ ],
  "blocks": [
    {
      "id": "...",
      "name": "DEVELOPMENT",
      "position": { "x": 420, "y": 48 },
      "size": { "width": 360, "height": 260 },
      "theme": null,
      "roomId": "default",
      "items": [
        {
          "id": "...",
          "name": "Cursor",
          "type": "application",
          "target": "C:\\\\Tools\\\\Cursor\\\\Cursor.exe",
          "icon": ""
        }
      ]
    }
  ]
}
```

## Security

- Target must be an **absolute path** chosen by the user (drag/drop or future picker).
- No command-line arguments, no PowerShell scripting, no admin elevation.
- Launch via documented `ProcessStartInfo.UseShellExecute = true` only.
- Forbidden: Explorer/WorkerW/Taskbar COM/shell injection/undocumented APIs.

## UX (V1)

- **Ctrl+Shift+B** or debug chrome **Add Block** → name / position / size dialog
- Drag Block chrome to move; corner grip to resize; **Del** removes Block
- Drop `.exe` / `.lnk` / files / folders onto a Block to add items
- Click item → Platform launches that path
- Theme: room `ThemeDefinition` (Block.Theme reserved for later)

## Shared with widgets

- Same canvas + `SetWindowRgn` input shaping
- Same theme tokens via `ThemePainter`
- Drag/resize patterns mirror `WidgetFrame` **without** a shared base class yet (one implementation first)
