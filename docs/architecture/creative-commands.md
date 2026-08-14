# Creative Commands

Allowed Creative Workspace operations for UI and **future AI**.

## Boundary

```
Natural language (future)
        ↓
Secret Base AI (future)
        ↓
CreativeCommand          ← registered ItemId / ProjectId / ResourceId / query / notes only
        ↓
CreativeCommandService   ← validate; mark opened / recent; no OS launch here
        ↓
CreativeWorkspaceService / CreativeProjectService
        ↓
Host ITargetLaunchService or system browser (only when ShouldLaunch)
```

AI must never pass arbitrary filesystem paths, run shell, elevate, or delete OS files.

## Command kinds

| Kind | Payload | Effect |
|------|---------|--------|
| `SearchItems` | `Query` | Search registered file/folder items |
| `OpenItem` / `OpenFolder` / `OpenProject` | `ItemId` | Workspace item launch (legacy Project = folder mark) |
| `ToggleFavorite` | `ItemId` | Flip item favorite |
| `SearchProjects` | `Query` | Search `CreativeProject` |
| `OpenCreativeProject` | `ProjectId` | Dashboard entry — mark opened, **no auto-launch** |
| `OpenCreativeProjectRoot` | `ProjectId` | Launch root + record Recent |
| `OpenCreativeProjectResource` | `ProjectId` + `ResourceId` | Launch path/https + record Recent |
| `ToggleCreativeProjectFavorite` | `ProjectId` | Flip project favorite |
| `DeleteCreativeProjectRegistration` | `ProjectId` | Remove registration only |
| `SaveCreativeProjectNotes` | `ProjectId` + `Notes` | Persist plain-text notes |
| `ToggleCreativeProjectResourceQuickAction` | `ProjectId` + `ResourceId` | Pin/unpin Quick Action |

## Not in scope

FS delete/move/rename, batch, “run exe with args”, process kill, AI implementation, Windows Search.
