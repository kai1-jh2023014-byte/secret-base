# Music Commands

Allowed music operations for Secret Base UI and **future AI**.

## Why Commands exist

AI (and any automation) must not:

- Call `IMusicProvider` directly
- Touch the filesystem, shell, registry, or processes
- Inject into WebView / Host Bridge
- Invent Spotify/YouTube API calls when Capabilities say no

Instead:

```
Natural language (future)
        ↓
Secret Base AI (future)  — maps intent → MusicCommand only
        ↓
MusicCommand
        ↓
MusicCommandService      — validate + capability check
        ↓
IMusicProvider           — Demo / future Spotify / YouTube / Local
```

## Command kinds

| Kind | Payload | Required capability |
|------|---------|---------------------|
| `SearchTrack` | `Query`, optional `ProviderId` | `Search` |
| `PlayTrack` | `Track` or `TrackId` + `ProviderId` | `Playback` |
| `Pause` | — | `Pause` |
| `Resume` | — | `Resume` |
| `Next` | — | `Next` |
| `Previous` | — | `Previous` |

Factory helpers: `MusicCommand.SearchTrack(...)`, `PlayTrack(...)`, `Pause()`, …

## Validation (MusicCommandService)

- Empty / oversized queries rejected (`MaxQueryLength`)
- Queries containing `://`, `..`, or absolute path prefixes rejected
- Missing track / unsupported capability → failed `MusicCommandResult` (no throw to UI)
- Aggregates search across providers that advertise `Search`

## Result

`MusicCommandResult`: `Succeeded`, `ErrorMessage`, `Tracks`, `CurrentTrack`, `IsPlaying`.

## Not in scope

- Volume / mute OS mixer Commands
- Arbitrary “run program” Commands
- Plugin marketplace Commands
- Connecting a real LLM in this milestone

When AI ships, it should only emit these MusicCommands (plus other explicitly designed, audited command families).
