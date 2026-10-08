# Progress / Genesis Widget

Desktop widget that mirrors the personal **Progress** dashboard and **Genesis MusicLab**.

## Truth source (do not invent %)

| Track | Real system | Metric the widget must show |
|-------|-------------|-----------------------------|
| **Progress** | [progress](https://github.com/kai1-jh2023014-byte/progress) UI「あなたの現在地」 | **Professional Readiness %** ・ **必須Skill completed/total** (+ Skill Map tip line) |
| **Genesis** | `~/genesis` MusicLab (`Start/Stop-MusicLab.bat`) | Explicit `percent` from status JSON/API only; otherwise **0%** (launchers ≠ progress) |

If Progress shows `Professional Readiness 0% ・ 必須Skill 0/29`, the Secret Base widget must also show **0%** — never Arena/demo/guessed values.

## Progress API probe order

Base default: `http://127.0.0.1:8001`

1. Readiness/dashboard (first hit wins):  
   `/api/readiness`, `/api/dashboard`, `/api/progress`, `/api/skills/summary`, `/api/me/progress`
2. Legacy fallback: `/api/problems` + `/api/attempts` (unique passed ÷ catalog)

Example readiness payload (matches the Progress dashboard):

```json
{
  "professionalReadinessPercent": 0,
  "requiredSkillsCompleted": 0,
  "requiredSkillsTotal": 29,
  "skillMap": [
    { "name": "Programming Fundamentals", "percent": 0 },
    { "name": "Python", "percent": 0 }
  ]
}
```

## Genesis / MusicLab — what to do on the Genesis side

Secret Base cannot invent MusicLab %. Have the lab write a status file.

**Recommended path (any one):**

1. `~/genesis/genesis-status.json` (WSL: `/home/kabuya/genesis/genesis-status.json`)
2. `%LocalAppData%\SecretBase\settings\genesis-status.json`
3. Widget config `GenesisStatusUrl` → localhost JSON

**Write from the bat scripts** (append near the end of each):

`Start-MusicLab.bat` — after the lab is up:

```bat
powershell -NoProfile -Command ^
  "$p=Join-Path $env:USERPROFILE 'genesis\genesis-status.json'; ^
   if (-not (Test-Path (Split-Path $p))) { $p='\\wsl.localhost\Ubuntu\home\kabuya\genesis\genesis-status.json' }; ^
   @{ phase='MusicLab'; status='Running'; percent=0; running=$true; updatedAt=(Get-Date).ToString('o') } ^
   | ConvertTo-Json | Set-Content -Encoding utf8 $p"
```

`Stop-MusicLab.bat` — after shutdown:

```bat
powershell -NoProfile -Command ^
  "$p=Join-Path $env:USERPROFILE 'genesis\genesis-status.json'; ^
   if (-not (Test-Path (Split-Path $p))) { $p='\\wsl.localhost\Ubuntu\home\kabuya\genesis\genesis-status.json' }; ^
   @{ phase='MusicLab'; status='Stopped'; percent=0; running=$false; updatedAt=(Get-Date).ToString('o') } ^
   | ConvertTo-Json | Set-Content -Encoding utf8 $p"
```

When you have a real MusicLab progress metric, set `"percent"` to that value (0–100).
Until then, `running` / `status` still update the Genesis line; percent stays **0**.

```json
{
  "phase": "MusicLab",
  "status": "Session open",
  "percent": 0,
  "running": true
}
```

## Display modes

| `displayMode` | Look |
|---------------|------|
| `full` (default) | Card chrome, bars, status, milestones |
| `minimal` | Transparent background; two lines only — `Progress  N%` / `MusicLab  N%` |

Toggle with the ◇ / ◆ button on the widget (persisted in layout JSON).

## Other sources

| `Source` | Notes |
|----------|--------|
| `personal-systems` (default) | Progress + Genesis above |
| `local` | AppData JSON (empty 0% when missing — no demo seed) |
| `http` | Custom snapshot JSON |
| `agent-arena` | Optional only — not the Progress/Genesis product |

## Config

```json
{
  "schemaVersion": 3,
  "source": "personal-systems",
  "progressApiBase": "http://127.0.0.1:8001",
  "genesisRoot": null,
  "genesisStatusUrl": null,
  "refreshSeconds": 60
}
```
