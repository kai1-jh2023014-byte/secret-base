# Personal AI OS (v1.0)

Secret Base is a **personal computing environment**. Chat is one entrance, not the product.

```
Observe → Understand → Remember → Predict → Prepare → Suggest → Confirm → Act → Learn
```

Base AI is the intelligence layer across Context, Memory, Situation, User State, Intent, Workspace, Activity, Search, Automation, Tools, and Feedback. It is not a chatbot bolted onto widgets.

## Loop (where it lives)

| Step | Implementation |
|------|----------------|
| Observe | `IComputerObservationService` (Windows foreground/idle) → `ObservationEvent` → `ObservationNormalizer` |
| Understand | `SituationComposer` + `UserStateComposer` + `IntentEngine` (evidence + confidence) |
| Remember | `IMemoryStore` Remember / RecallRanked / Update / Merge / Expire / Forget |
| Predict | `IntentEngine` + `ProjectContinuation` + `DailyBriefingComposer` |
| Prepare | `WorkspacePreparer` (Safe Auto — no launch) |
| Suggest | `AutomationEngine` + `AttentionCenter` + InterventionPolicy |
| Confirm | Assistant pending confirmation + Continue dialog. App launch never skips this. |
| Act | Registered tools only. Safety Gate. Allowlist. |
| Learn | `LearningLoop` + `SuggestionRanking`. **Never** Confirmation → Auto Action. |

## Product surfaces

| Surface | What it is |
|---------|------------|
| Personal Space | Overlay / Mac workspace. Default layout remains Clock + Text. Base card is situation, not a widget dump. |
| Command Center | `CommandCenter.Handle` — briefing, continue, focus, search, capture, timeline, explain. LLM last. |
| Command Palette | Ctrl+Space. Ranked continue / briefing / focus / capture / search. Keyboard first. |
| Quick Capture | Classify Idea/Todo/Note/Memory/Project. User confirms destination. Secrets refused. |
| Daily Briefing | Structured calendar + todos + previous session. Not an LLM summary. |
| Attention Center | Quiet ranked items. Silent during focus and quiet hours. |
| Activity Timeline | Meaningful day view after dedup + aggregation. |
| Memory UI | What Secret Base remembers — search, source, importance, expiry, Forget. |
| Privacy Center | Observed / Remembered / Allowed / Requires confirmation / Never collected. Quiet hours settings. |
| Automation Permission | Enable/disable persisted rules. Cooldown. Last run. |

## Command Center (no LLM for simple work)

Utterances such as 「今日何すればいい？」, 「昨日の続きをやりたい」, 「30分集中したい」, 「今日何してた？」, 「なんでこれを提案したの？」 are routed in Core. Continue still opens a confirmation (`workspace_continue`). Focus is Safe Auto (timer only).

## Safety (unchanged lanes)

- **Safe Auto** — prepare, focus timer, memory write, capture, briefing
- **Confirmation** — app launch, file open, calendar write, music play, workspace continue
- **Explicit** — unregister / return Block (`files_delete`). Never OS delete of user files.

No arbitrary shell, PowerShell, or exe. No always-on LLM. `LearningPolicy.MayEscalatePrivilege = false`. 20 accepted Continues ≠ auto-launch.

## Observation

- **Windows:** process name + idle via documented user32 APIs. Browser titles dropped unless they name a registered project.
- **macOS:** `NullComputerObservationService` — honest Unsupported. Secret Base still records its own activity.
- **Never:** keystrokes, clipboard, passwords, browser page contents, mic, camera, screenshots, arbitrary document bodies.

## Persistence

`%LocalAppData%\SecretBase\settings\` (macOS: Application Support):

- `memory.json` — schema 1, Update/Merge/Expire
- `activity.json`
- `automation-feedback.json`
- `sessions.json`
- `automation-rules.json` — schema 1, defaults restored on corruption
- `base-settings.json` — quiet hours, default focus, intervention prefs

Atomic `.tmp` writes. Corrupt JSON → empty/default. Existing v0.x layouts, todos, themes, memories, sessions are migrated in place (schema bump, no wipe).

## AI outage

Clock, Calendar, Todo, Search, Memory, Activity, Workspace, Command Palette, Automation, Daily Briefing, and basic suggestions keep working when remote and local providers are unavailable.

## Default layout

Existing and first-run layouts stay **Clock + Text** (2 widgets). Base / Palette / Briefing are overlays and dialogs, not extra default widgets.
