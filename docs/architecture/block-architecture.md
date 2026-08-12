# Block Architecture

Blocks are small **rooms on the Desktop** — movable/resizable hosts for launchable items (apps, shortcuts, files, folders).

They are **not** Windows folders and do not replace Explorer.

## Layering

| Layer | Owns |
|-------|------|
| **Core** | `Block`, `BlockItem`, `BlockItemType`, `BlockTargetValidator`, `DefaultBlockFactory` |
| **Infrastructure** | Layout JSON (`blocks` array), schema v1→v2 migration |
| **Platform.Abstractions** | `ITargetLaunchService`, `IFileIconService`, `IBlockItemIntakeService` |
| **Platform.Windows** | Launch; icons; **move Desktop .lnk into block-items storage** |
| **App** | `BlockFrame` (+ **Grid** arrange, drop/move intake), Add Block **+** button |

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

- Always-visible **+** button (bottom-left) opens Add Block dialog
- Block header **Grid** button arranges icons evenly; also auto-arranges after drop / resize
- Drop **Desktop shortcuts (.lnk)** → **moved** into `%LocalAppData%\SecretBase\block-items\` (Desktop original removed — no duplicate)
- Drop Program Files `.exe` → path **link** only (binary is not relocated)
- Real shell icons via `IFileIconService`; click to launch; drag to fine-tune then **Grid** to re-even

## Shared with widgets

- Same canvas + `SetWindowRgn` input shaping
- Same theme tokens via `ThemePainter`
- Drag/resize patterns mirror `WidgetFrame` **without** a shared base class yet (one implementation first)
