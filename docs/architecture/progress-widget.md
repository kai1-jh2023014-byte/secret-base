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

## Genesis / MusicLab

Probes folder + Desktop bats for *presence*, but **percent stays 0** until
`genesis-status.json` (or `GenesisStatusUrl`) supplies `"percent"`.

```json
{
  "phase": "MusicLab",
  "status": "Session open",
  "percent": 0,
  "running": true
}
```

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
