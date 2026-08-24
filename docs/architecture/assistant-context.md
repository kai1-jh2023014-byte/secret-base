# Assistant Context

Read-only snapshot over existing Commands/Services. Never launches OS and never includes secrets, tokens, or absolute paths.

## Service

`IAssistantContextService` / `AssistantContextService`

| Method | Notes |
|--------|-------|
| `GetSnapshotAsync(scope)` | Scoped aggregation |
| `GetProjectAsync(id)` | Registered projects only |
| `GetMusicState()` | Capabilities + demo-catalog honesty |
| `GetProviderStatus()` | Connected / unavailable + masked key flag |

## Structured snapshot

`AssistantContextSnapshot`

- `CurrentTime` via `CapturedAt`
- `TodayEvents` / `UpcomingEvents` / `FreeTimeSlots`
- `Projects` / `RecentProjects`
- `Apps` (with `HasProjectRoot` bool — **no path**)
- `Music`
- `Integrations` (domain names)
- `Provider` (id, model, status, maxSteps)

## Context compression

`AssistantContextSelector` picks minimal scopes from user text + intent:

| Utterance | Scope |
|-----------|-------|
| 今日の予定 | Calendar |
| Pokemon Projectについて | Creative |
| 今日何をするべき？ | Calendar + Creative |
| 作業モード | Calendar + Creative + Music |

`FormatForModel` emits only sections in scope. Full dump of the PC is avoided.

## Free time

`ComputeFreeTime` derives gaps in work hours (default 09:00–18:00) from non-all-day today events. Candidates only — not a life coach.

## Secret exclusion

Formatter and tool results must not contain:

- API keys / Bearer tokens / passwords
- Absolute filesystem paths
- Unnecessary internal identifiers beyond registered project/app ids needed for tools

## Related

- [ai-assistant.md](ai-assistant.md)
- [assistant-tools.md](assistant-tools.md)
- [assistant-planning.md](assistant-planning.md)
- [assistant-security.md](assistant-security.md)
