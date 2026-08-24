# Assistant Planning

Thin planning layer for Secret Base AI v0.5. **Not** a workflow engine or autonomous agent.

## Loop

```
Context → Plan → Confirmation → Action → Result
```

## Intent

`AssistantIntentClassifier` (heuristic):

| Kind | Examples |
|------|----------|
| Question | 「Pokemonについて教えて」 |
| Suggestion | 「今日何をやればいい？」 |
| ActionRequest | 「Pokemonを開いて」「開発を始めよう」 |

「教えて」 must not become ActionRequest.

## Models

- `AssistantPlan` / `AssistantPlanStep` — UI-visible steps (Read / Suggest / ConfirmAction)
- `AssistantPendingAction` — queued confirm items
- `AssistantActionResult` — honest post-Run outcomes

## Planner

`AssistantPlanner` builds small plans for known flows:

- Today priority → calendar + projects + `schedule_recommend`
- Start project → calendar + project check + Cursor confirm step

Steps are clamped by `AssistantSettings.MaxSteps` (default **5**, hard cap **8**).

## Autonomy ceiling

**Allowed:** ReadOnly context, Suggest tools, Plan UI, propose confirm actions, execute existing Commands after Run.

**Forbidden:** shell, PowerShell, arbitrary process/files, Computer Use, infinite loops, credential reads, security bypass.

## Session memory

In-memory only. Last mentioned project helps resolve 「さっきの」. Cap 20 visible messages. No long-term memory.

## Untrusted context data

Calendar titles, project descriptions/notes, app descriptions, and music metadata are treated as **untrusted data**, not instructions. They may influence suggestions, but they must not override system/developer policy or become implicit commands.

## Related

- [ai-assistant.md](ai-assistant.md)
- [assistant-security.md](assistant-security.md)
