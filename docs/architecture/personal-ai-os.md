# Personal AI OS (v0.7)

Secret Base is a **Personal AI Computing Environment**. Chat is one entrance, not the product.

```
User → Personal Space → Base AI
         Context + Memory + Intent → User State
         → Workspace Engine → Automation Engine → Safety Gate → Actions
         → Result / Feedback → Memory / Context
```

## Layers (Core)

| Piece | Role |
|-------|------|
| Memory | Durable facts with scope, importance, TTL. Refuses secrets and paths. |
| Activity | Named events → meaningful work ("Worked on Secret Base"). No OS hooks. |
| User State | Live picture + heuristic confidence. |
| Intent | ContinueProject / Focus / Unknown… Intent is never an Action. |
| Automation | Trigger → condition → intent → suggestion. Quiet during Focus. |
| Continuation | Last session + next task from memory, activity, workspace, todos. |
| Search | Deterministic metadata over memory/activity/projects/files/calendar/todos. |

## Safety

Unchanged lanes:

- **Safe Auto** — prepare, focus timer, memory write, suggestions, UI
- **Confirmation** — app launch, file open, calendar write, music play, workspace continue
- **Explicit** — unregister / return Block to Desktop (`files_delete`). Never OS `File.Delete`.

No arbitrary shell, PowerShell, or exe. No always-on LLM.

## Persistence

`%LocalAppData%\SecretBase\settings\` (macOS: Application Support):

- `memory.json`
- `activity.json`
- `automation-feedback.json`

Corrupt JSON → empty/default. Atomic `.tmp` writes.

## UX

The **Base** Mini App (`WidgetTypes.Dashboard`) is catalog/onboarding only. Existing layouts keep Clock+Text. Clock stays two extra lines (next event / status).
