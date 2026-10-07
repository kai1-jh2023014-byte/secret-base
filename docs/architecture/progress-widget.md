# Progress / Genesis Widget

Desktop widget that shows **Progress** (overall %) and **Genesis** advancement
(phase, stage, %, milestones).

## Live data source (default)

Verified fetch (2026-10-07) from the public Agent Arena API on Base Sepolia:

| Endpoint | Example |
|----------|---------|
| `GET /health` | `{"ok":true}` |
| `GET /winners/summary` | `{"rounds":{"wins":40,"losses":22},"minted":{"total":36,"genesis":12,"ascension":24}}` |
| `GET /winners/top` | leaderboard (optional personal wallet) |
| `GET /rounds/current` | queue counters |

Default API root: `https://agent-arena-api.agentarenaonbase.workers.dev`

Mapping (global):

- **Progress** = total minted / 3500 (500 Genesis + 3000 Ascension), win/loss caption
- **Genesis** = Genesis minted / 500, phase/stage + supply milestones

Optional widget config `WalletAddress` (`0x…`) switches Progress to personal identity
completion (1 Genesis + 5 Ascensions) using `/winners/top`.

## Other sources

| `Source` | Behavior |
|----------|----------|
| `agent-arena` (default) | Live Arena API; falls back to local JSON on failure |
| `local` | `%LocalAppData%\SecretBase\settings\progress-genesis.json` |
| `http` | Custom HTTPS JSON (same schema as local); falls back to local |

Writes to the local store use `.tmp` → copy over target → delete `.tmp`.

## Architecture

```
ProgressWidgetView (WinUI)
        ↓
IProgressGenesisProvider
   ├─ AgentArenaProgressGenesisProvider  → /winners/summary (+ /top)
   ├─ LocalJsonProgressGenesisProvider   → JsonProgressGenesisStore
   └─ HttpProgressGenesisProvider        → HTTPS + local fallback
        ↓
AgentArenaProgressMapper (Core) → ProgressGenesisSnapshot
```

## Widget configuration (camelCase in layout JSON)

```json
{
  "schemaVersion": 2,
  "source": "agent-arena",
  "walletAddress": null,
  "arenaApiBase": null,
  "remoteUrl": null,
  "refreshSeconds": 60,
  "showMilestones": true
}
```

## UX

Add via **Add Widget** → Information → **Progress / Genesis**.
Refresh button + auto-refresh (default 60s, configurable 15–600).

## Security

- No Host Bridge / WebView
- Agent Arena + custom HTTP require HTTPS
- Wallet address is public on-chain identity only (no private keys)
- No secrets in the snapshot JSON
