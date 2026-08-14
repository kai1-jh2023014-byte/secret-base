# Creative Workspace

A desk for **Projects** with **Dashboards** (creative activity bases) plus favorite files/folders — **not** a Windows Explorer replacement.

## Purpose

> Open a Project and know what you are making, what to open next, and how to resume.

Projects group root folders, files, folders, and https links. The **Project Dashboard** shows Quick Actions, Recent (Secret Base opens only), Resources, and Notes.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"creative"` |
| Widget config | `CreativeWorkspaceWidgetConfiguration` (UI prefs only) |
| Workspace data | `%LocalAppData%\SecretBase\creative\workspace.json` |
| Projects data | `%LocalAppData%\SecretBase\creative\projects.json` (`schemaVersion` **2**) |
| View | `CreativeWorkspaceView` (list → Dashboard) |

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

## Project Dashboard (MVP)

```
Header (name / type label / favorite / description)
Quick Actions   ← Root (if set) + resources with IsQuickAction
Recent          ← Secret Base opens only (no FS watcher / Windows Search)
Resources       ← Files / Folders / Links
Notes           ← plain text + Save
Edit / Add / Remove registration / Back
```

Project Type (`Music`, `Programming`, …) drives glyph + label only — not separate UIs or forced apps.

## Security

- Open / edit metadata / add-remove **registration** / favorite / notes / quick-action pin
- **Never** delete/move/rename OS files; no PowerShell; no Host Bridge; no Overlay changes
- External links: `WebUrlValidator` → system browser
- Commands take ids only — see [creative-commands.md](creative-commands.md)

## Persistence (`projects.json`)

```json
{
  "schemaVersion": 2,
  "projects": [
    {
      "id": "...",
      "name": "My First Song",
      "description": "...",
      "projectType": "music",
      "rootFolder": "D:\\Music\\MyFirstSong",
      "isFavorite": true,
      "notes": "サビをもう少し盛り上げる",
      "resources": [
        {
          "id": "...",
          "name": "Lyrics",
          "kind": "file",
          "target": "D:\\Music\\MyFirstSong\\lyrics.txt",
          "isQuickAction": true
        }
      ],
      "recentItems": [
        {
          "key": "...",
          "name": "Lyrics",
          "kind": "file",
          "target": "D:\\Music\\MyFirstSong\\lyrics.txt",
          "openedAt": "…"
        }
      ]
    }
  ]
}
```

`CreativeProjectDocumentMigrator` upgrades `schemaVersion` 1 → 2 (Dashboard fields default empty / false).

## Explicitly not in MVP

AI, FS watcher, Windows Search, Git/Notion/Drive/Spotify APIs, auto app detection, Kanban/Todo, Markdown editor, Collaboration, Cloud sync, type-specific mega UIs, Overlay changes.

## Windows manual checklist

1. Overlay / Exit / other widgets unchanged (**unverified on Linux agent**).
2. CW → Project → Dashboard shows Quick Actions / Recent / Resources / Notes.
3. ★ on resource pins Quick Action; Open records Recent; Save Notes persists.
4. Remove resource / Remove Project = registration only; OS files remain.
5. Restart restores notes, quick actions, recent.
