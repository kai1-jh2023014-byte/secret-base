# Integration events

Declared in the manifest, for example `game.finished`, `task.completed`, `project.created`.

Normalized event:

| Field | Purpose |
|-------|---------|
| `source` | Display name |
| `integrationId` | Registry id |
| `eventType` | Declared type |
| `timestamp` | Event time |
| `safePayload` | Redacted, truncated JSON/summary |
| `correlationId` | Dedup / Activity link |

Flow:

```
External app → Connector event / Webhook
    → Event Normalizer
    → Activity (not Memory)
    → optional Automation suggestion (still Confirm for launches)
```

External data is **not** remembered unless you explicitly save it.

Automation trigger: `AutomationTriggerKind.IntegrationEvent` with optional `integrationId` + `integrationEventType`. Cooldown still applies. Rules cannot skip Safety.
