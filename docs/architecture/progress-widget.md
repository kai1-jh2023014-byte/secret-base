# Progress / Genesis Widget

Desktop widget that shows **Progress** (overall %) and **Genesis** advancement
(phase, stage, %, milestones).

## Assumed data source (MVP)

No external Progress/Genesis API was found in-repo, prior PRs, or Cursor cloud
agent history. This MVP uses:

| Priority | Source | Path / shape |
|----------|--------|----------------|
| Default | Local JSON | `%LocalAppData%\SecretBase\settings\progress-genesis.json` (macOS: `~/Library/Application Support/SecretBase/settings/progress-genesis.json`) |
| Optional | HTTPS JSON | Widget config `RemoteUrl` — same JSON schema; falls back to local on failure |

First missing local file is seeded with a demo snapshot so Add Widget is immediately useful.
Edit the JSON (or point `RemoteUrl` at a compatible endpoint) to drive real values.

Writes use the standard atomic pattern: `.tmp` → copy over target → delete `.tmp`.

## Architecture

```
ProgressWidgetView (WinUI)
        ↓
IProgressGenesisProvider
   ├─ LocalJsonProgressGenesisProvider  → JsonProgressGenesisStore
   └─ HttpProgressGenesisProvider       → HTTPS + local fallback
        ↓
ProgressGenesisSnapshot (Core)
   ├─ ProgressTrack
   └─ GenesisTrack (+ GenesisMilestone[])
```

- **Core:** models, `IProgressGenesisProvider` / `IProgressGenesisStore`, formatter, widget config
- **Infrastructure:** JSON store + local/HTTP providers + factory
- **Widgets:** `ProgressWidgetView`
- **App:** Desktop host wiring (catalog / factory / `CreateWidgetContent`)

## JSON schema (camelCase)

```json
{
  "schema": 1,
  "updatedAt": "2026-10-07T12:00:00+00:00",
  "sourceKind": "local-json",
  "progress": {
    "title": "Progress",
    "percent": 28,
    "status": "On track",
    "detail": "Optional detail line"
  },
  "genesis": {
    "title": "Genesis",
    "phase": "Foundation",
    "stage": 2,
    "stageCount": 5,
    "percent": 35,
    "status": "Building",
    "milestones": [
      { "id": "…", "label": "Define model", "isComplete": true },
      { "id": "…", "label": "Desktop widget", "isComplete": false }
    ]
  }
}
```

## UX

Add via **Add Widget** → Information → **Progress / Genesis**. Not seeded on first run.
Refresh button + auto-refresh (default 60s, configurable `RefreshSeconds` 15–600).

## Security

- No Host Bridge / WebView
- HTTP provider requires HTTPS except localhost
- No secrets in the snapshot JSON
