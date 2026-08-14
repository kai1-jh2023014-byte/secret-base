# Creative Commands

Allowed Creative Workspace operations for UI and **future AI**.

## Boundary

```
Natural language (future)
        ↓
Secret Base AI (future)
        ↓
CreativeCommand          ← registered ItemId / query only
        ↓
CreativeCommandService   ← validate; mark Recent; no OS launch here
        ↓
CreativeWorkspaceService
        ↓
Host ITargetLaunchService (only when ShouldLaunch)
```

AI must never:

- Pass arbitrary filesystem paths to open/delete
- Run PowerShell / cmd / free-form Process arguments
- Elevate
- Call Win32 directly

## Command kinds

| Kind | Payload | Effect |
|------|---------|--------|
| `SearchItems` | `Query` | Search registered items |
| `OpenItem` | `ItemId` | Mark Recent + request launch |
| `OpenFolder` | `ItemId` | Same; item must be Folder/Project |
| `OpenProject` | `ItemId` | Same; item must be Project |
| `ToggleFavorite` | `ItemId` | Flip favorite |

`CreativeCommandResult.ShouldLaunch` tells the host to call the existing safe launcher.

## Not in scope

Delete / Move / Rename / Batch / “Run this exe with args” / volume / process kill.
