# Personal AI OS (v0.8)

Secret Base is a **Personal AI Computing Environment**. Chat is one entrance, not the product.

```
REAL WORLD / COMPUTER
        ↓
Observation (privacy-first names only)
        ↓
Activity → Context + Memory
        ↓
Situation (evidence) → User State → Intent (never an Action)
        ↓
Workspace / Session → Automation pipeline
        ↓
Intervention Policy (quiet by default)
        ↓
Safety Gate → Confirmation if required → Action
        ↓
Feedback → Ranking (never privilege escalation) → Memory / User Model
```

Base AI sits on top of this loop as an **intelligence layer**, not a chatbot.

## Layers (Core)

| Piece | Role |
|-------|------|
| Observation | Sanitized computer events (process name, idle, system resume). No keystrokes, clipboard, or file contents. |
| Activity | Named events → meaningful work. Deduplicated. |
| Memory | Remember / ranked Recall / Expire / Forget. Refuses secrets and paths. |
| Situation | Grounded `CurrentSituation` with evidence and confidence. |
| User State | Live picture + heuristic confidence. |
| Intent | ContinueProject / ResumePreviousSession / Focus / Unknown… Intent is never an Action. |
| Session | Deterministic work-session summaries (no LLM required). |
| Workspace | Prepare vs Continue. Continue still requires confirmation and does not auto-launch apps. |
| Automation | Trigger → Condition → Intent → Plan → Safety → Suggestion → Result. |
| Intervention | Silent / Passive / Suggest / Confirm. Focus and 22:00–08:00 stay quiet unless a live calendar block raises urgency. |
| Learning | Accept/dismiss changes ranking and can quiet repeats. **Never** Confirmation → Auto Action. |
| Search | Deterministic metadata over memory, activity, projects, files, calendar, todos, workspace, sessions. |

## Safety

Unchanged lanes:

- **Safe Auto** — prepare, focus timer, memory write, suggestions, UI
- **Confirmation** — app launch, file open, calendar write, music play, workspace continue
- **Explicit** — unregister / return Block to Desktop (`files_delete`). Never OS `File.Delete`.

No arbitrary shell, PowerShell, or exe. No always-on LLM. Learning cannot escalate privilege (`LearningPolicy.MayEscalatePrivilege = false`).

## Observation (Windows)

`IComputerObservationService` lives in Platform.Abstractions.

- **Windows:** `WindowsForegroundObservationService` — documented `GetForegroundWindow` / `GetWindowText` / process name / `GetLastInputInfo`. 2.5s timer, idle after 3 minutes. Browser titles are dropped unless they name a registered project.
- **macOS / tests:** `NullComputerObservationService` — honest no-op. Secret Base still records its own activity.

Core never calls Win32.

## Persistence

`%LocalAppData%\SecretBase\settings\` (macOS: Application Support):

- `memory.json`
- `activity.json`
- `automation-feedback.json`
- `sessions.json`

Corrupt JSON → empty/default. Atomic `.tmp` writes.

## UX

The **Base** Mini App (`WidgetTypes.Dashboard`) is catalog/onboarding only. Existing layouts keep Clock+Text. Base shows the current situation — greeting, calendar/session, last work, next task, Continue — not a widget dump. Clock stays two extra lines (next event / status).

## AI outage

Clock, Calendar, Todo, Memory, Activity, Workspace, Intent, Automation, and Search keep working when remote and local providers are unavailable.
