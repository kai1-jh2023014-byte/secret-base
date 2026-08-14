# Creative Workspace

A desk for the projects, folders, and files you return to often — **not** a Windows Explorer replacement.

## Purpose

> The fastest way back to what you are making.

Favorites + Recent + Search over **registered** items only. Open with Windows defaults after an explicit click.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"creative"` |
| Widget config | `CreativeWorkspaceWidgetConfiguration` (UI prefs only) |
| Workspace data | `%LocalAppData%\SecretBase\creative\workspace.json` |
| View | `CreativeWorkspaceView` |

## How to add

- **CW** FAB / debug **Add Creative** / **Ctrl+Shift+E**

## Architecture

```
Creative Workspace UI
        ↓
CreativeCommandService   ← future AI entry
        ↓
CreativeWorkspaceService ← favorites / recent / search / register
        ↓
ICreativeWorkspaceStore  ← JSON AppData
        ↓
(on Open) ITargetLaunchService  ← existing Block launcher (UseShellExecute)
```

Path pickers: `IPathPickService` → `WindowsPathPickService` (standard FileOpenPicker / FolderPicker).

## MVP features

1. Add File / Add Folder (optional “as Project”)
2. Favorites (★)
3. Recent (max 10)
4. Search registered Name / Path / Type only
5. Open registered item → Windows association / Explorer
6. Missing path → “見つかりません” (item kept)
7. Theme + WidgetFrame move/resize
8. Persistence with `schemaVersion: 1`

## Explicitly not in MVP

Full Explorer, in-widget folder browse, delete/move/rename, batch ops, FS watcher, whole-PC search, PowerShell, elevation, AI, cloud drive sync, Git.

## Security

```
User adds path (picker)
        ↓
Workspace stores reference
        ↓
User clicks registered item
        ↓
CreativeCommand.OpenItem (id only — never free-form AI paths)
        ↓
ITargetLaunchService (documented shell open)
```

- No Host Bridge
- No arbitrary command lines (reuses `BlockTargetValidator`)
- Commands never delete/move/rename
- Future AI must emit `CreativeCommand` only — see [creative-commands.md](creative-commands.md)

## Persistence fragment

```json
{
  "schemaVersion": 1,
  "items": [
    {
      "id": "...",
      "name": "Secret Base",
      "path": "C:\\Users\\…\\secret-base",
      "itemType": "Project",
      "isFavorite": true,
      "dateAdded": "…",
      "lastOpened": "…"
    }
  ]
}
```

Widget layout JSON only stores type/geometry + small UI flags — **not** the item list.

## Windows manual checklist

1. Overlay click-through / Exit / restore still OK (**unverified on Linux agent**).
2. Clock / Text / Web / Calendar / Music / Blocks unchanged.
3. CW → Add File / Folder → appears in list.
4. ★ favorite / search / open / Recent update.
5. Delete file outside Secret Base → open shows 見つかりません.
6. Restart restores workspace.json + widget presence.
