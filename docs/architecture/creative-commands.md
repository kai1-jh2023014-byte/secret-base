# Creative Commands

Allowed Creative Workspace operations for UI and **future AI**.

## Boundary

```
Natural language (future)
        ↓
Secret Base AI (future)
        ↓
CreativeCommand          ← registered ItemId / ProjectId / ResourceId / query only
        ↓
CreativeCommandService   ← validate; mark opened; no OS launch here
        ↓
CreativeWorkspaceService / CreativeProjectService
        ↓
Host ITargetLaunchService or system browser (only when ShouldLaunch)
```

AI must never:

- Pass arbitrary filesystem paths to open/delete
- Run PowerShell / cmd / free-form Process arguments
- Elevate
- Call Win32 directly
- Delete/move/rename OS files (even via “Delete Project” — that is registration-only)

## Command kinds

| Kind | Payload | Effect |
|------|---------|--------|
| `SearchItems` | `Query` | Search registered file/folder items |
| `OpenItem` | `ItemId` | Mark Recent + request launch |
| `OpenFolder` | `ItemId` | Same; item must be Folder/legacy Project |
| `OpenProject` | `ItemId` | Legacy: item must be `CreativeItemType.Project` |
| `ToggleFavorite` | `ItemId` | Flip item favorite |
| `SearchProjects` | `Query` | Search `CreativeProject` registrations |
| `OpenCreativeProject` | `ProjectId` | Mark opened; launch root if present |
| `OpenCreativeProjectRoot` | `ProjectId` | Launch root folder (required) |
| `OpenCreativeProjectResource` | `ProjectId` + `ResourceId` | Launch path or https link |
| `ToggleCreativeProjectFavorite` | `ProjectId` | Flip project favorite |
| `DeleteCreativeProjectRegistration` | `ProjectId` | Remove Secret Base registration only |

`CreativeCommandResult.ShouldLaunch` + `LaunchTarget` (+ `LaunchIsExternalLink`) tell the host what to open.

## Not in scope

Delete / Move / Rename filesystem targets / Batch / “Run this exe with args” / volume / process kill / embedded Project WebView / AI implementation.
