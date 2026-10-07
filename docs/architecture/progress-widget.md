# Progress / Genesis Widget

Desktop widget that shows **Progress** (programming learning) and **Genesis**
(MusicLab) advancement.

## What Progress / Genesis are

| Track | Project | Where |
|-------|---------|--------|
| **Progress** | Personal programming learning system | [kai1-jh2023014-byte/progress](https://github.com/kai1-jh2023014-byte/progress) — FastAPI `:8001` |
| **Genesis** | Local MusicLab / creative stack | `~/genesis` (WSL: `/home/kabuya/genesis`) with `scripts/windows/Start-MusicLab.bat` + `Stop-MusicLab.bat` (often copied to Desktop) |

## Default data source (`personal-systems`)

### Progress (verified API shape)

Progress backend (`uvicorn app.main:app --reload --port 8001`):

| Endpoint | Use |
|----------|-----|
| `GET /api/health` | Liveness |
| `GET /api/problems` | Catalog size |
| `GET /api/attempts` | Solved / recent activity |

Mapping: unique `result == "passed"` problems ÷ problem count → Progress %.

### Genesis (local probe + optional status JSON)

Because Genesis is a private local tree (not a public GitHub API), the provider:

1. Resolves Genesis roots (`SECRETBASE_GENESIS_ROOT`, `~/genesis`, `/home/kabuya/genesis`, `\\wsl.localhost\Ubuntu\home\kabuya\genesis`, …)
2. Detects `scripts/windows/Start-MusicLab.bat` / Desktop copies
3. Optionally reads `genesis-status.json` (project root, `.secret-base/`, or `%LocalAppData%\SecretBase\settings\`)
4. Optionally `GET`s widget config `GenesisStatusUrl` / status file `healthUrl`

Example `genesis-status.json`:

```json
{
  "phase": "MusicLab",
  "status": "Session open",
  "percent": 55,
  "stage": 2,
  "stageCount": 4,
  "running": true,
  "healthUrl": "http://127.0.0.1:8787/health",
  "milestones": [
    { "label": "Lab bootstrapped", "isComplete": true },
    { "label": "First track exported", "isComplete": false }
  ]
}
```

Tip: have `Start-MusicLab.bat` / `Stop-MusicLab.bat` write/update this file so the widget stays accurate.

## Other sources

| `Source` | Behavior |
|----------|----------|
| `personal-systems` (default) | Progress API + Genesis probe |
| `local` | AppData `progress-genesis.json` only |
| `http` | Custom HTTPS JSON snapshot |
| `agent-arena` | Optional Base Sepolia Arena counters |

## Widget configuration

```json
{
  "schemaVersion": 3,
  "source": "personal-systems",
  "progressApiBase": "http://127.0.0.1:8001",
  "genesisRoot": null,
  "genesisStatusUrl": null,
  "refreshSeconds": 60,
  "showMilestones": true
}
```

## UX

**Add Widget → Information → Progress / Genesis**. Refresh + auto-refresh (15–600s).

## Security

- Progress HTTP only on localhost
- No private keys; Genesis paths are local filesystem probes only
- No Host Bridge / WebView
