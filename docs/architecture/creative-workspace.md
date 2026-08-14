# Creative Workspace

A desk for **Projects** (creative activity containers) plus favorite files/folders — **not** a Windows Explorer replacement.

## Purpose

> The fastest way back to what you are making.

Projects group root folders, files, folders, and https links for one creative activity. Files/Folders registrations remain available alongside Projects.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"creative"` |
| Widget config | `CreativeWorkspaceWidgetConfiguration` (UI prefs only) |
| Workspace data | `%LocalAppData%\SecretBase\creative\workspace.json` |
| Projects data | `%LocalAppData%\SecretBase\creative\projects.json` |
| View | `CreativeWorkspaceView` |

## How to add

- **CW** FAB / debug **Add Creative** / **Ctrl+Shift+E**

## Architecture

```
Creative Workspace UI
        ↓
CreativeCommandService   ← future AI entry (ids / queries only)
        ↓
CreativeWorkspaceService  +  CreativeProjectService
        ↓
ICreativeWorkspaceStore   +  ICreativeProjectStore  ← JSON AppData
        ↓
(on Open) ITargetLaunchService / system browser  ← host only
```

Path pickers: `IPathPickService` → `WindowsPathPickService` (standard FileOpenPicker / FolderPicker).

Legacy note: `CreativeItemType.Project` still means “folder marked as project” in `workspace.json`. Rich containers use `CreativeProject` in `projects.json`.

## Project model (MVP)

```
CreativeProject
├── Name / Description / Type (Music, Programming, Video, Design, Writing, Other)
├── RootFolder (optional absolute path)
├── Favorite / DateAdded / LastOpened
└── Resources[]  → File | Folder | ExternalLink
```

Type is metadata only — it does **not** force a specific app to launch.

## MVP features

1. Project CRUD: Create / Edit / Delete **registration only** / Open / Favorite
2. Project Detail: root, files, folders, external links
3. Add File / Folder / https link to a Project
4. Projects list in Creative Workspace (+ existing Favorites / Recent / Files & Folders)
5. Open → Windows association / Explorer / system browser
6. Missing path → “見つかりません” (registration kept)
7. Theme + WidgetFrame move/resize
8. Persistence with `schemaVersion: 1` on both JSON documents

## Explicitly not in MVP

Full Explorer, embedded WebView for Project links, delete/move/rename of OS files, batch ops, FS watcher, whole-PC search, PowerShell, elevation, AI, cloud drive sync, Git, type-forced app launches, Overlay changes.

## Security

```
User creates Project / adds resource (picker or https)
        ↓
projects.json stores references only
        ↓
User clicks Open (project / resource id)
        ↓
CreativeCommand (id only — never free-form AI paths)
        ↓
Host ITargetLaunchService or https browser open
```

- **Delete Project** removes Secret Base registration only — never deletes Windows files/folders
- No Host Bridge, no arbitrary command lines, no process kill, no registry/taskbar/Explorer hacks
- Future AI must emit `CreativeCommand` only — see [creative-commands.md](creative-commands.md)

## Persistence fragments

`creative/workspace.json` — file/folder item shortcuts (unchanged).

`creative/projects.json`:

```json
{
  "schemaVersion": 1,
  "projects": [
    {
      "id": "...",
      "name": "My First Song",
      "description": "My first original song.",
      "projectType": "music",
      "rootFolder": "D:\\Music\\MyFirstSong",
      "isFavorite": true,
      "dateAdded": "…",
      "lastOpened": "…",
      "resources": [
        {
          "id": "...",
          "name": "Project File",
          "kind": "file",
          "target": "D:\\Music\\MyFirstSong\\MyFirstSong.cwp"
        },
        {
          "id": "...",
          "name": "YouTube",
          "kind": "externalLink",
          "target": "https://www.youtube.com/..."
        }
      ]
    }
  ]
}
```

Widget layout JSON only stores type/geometry + small UI flags — **not** project lists.

## Windows manual checklist

1. Overlay click-through / Exit / restore still OK (**unverified on Linux agent**).
2. Clock / Text / Web / Calendar / Music / Blocks unchanged.
3. CW → **+ Project** → Name / Description / Type / Root → appears under ★ Projects.
4. Project Detail → Add File / Folder / Link → Open uses defaults / browser.
5. Remove from Secret Base → registration gone; OS files remain.
6. Restart restores `projects.json` + `workspace.json` + widget presence.
