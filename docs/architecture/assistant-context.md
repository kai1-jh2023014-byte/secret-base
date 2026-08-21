# Assistant Context Layer

Read-only snapshot of Secret Base state for Secret Base AI. Does **not** launch OS actions.

## Purpose

Let the model answer:

- 「今日何する予定だっけ？」
- 「今進めているプロジェクトは？」
- 「最近何を開いた？」
- 「登録してあるアプリは？」

by calling tools that read existing Commands / Services — not by dumping the whole PC into the prompt.

## Flow

```
AssistantService
 ↓
assistant_get_context / calendar_* / creative_* / apps_list / music_get_state
 ↓
IAssistantContextService  (or direct Command for single-domain reads)
 ↓
CalendarCommand / CreativeCommand / AppCommand / MusicService / IntegrationCatalog
```

## `IAssistantContextService`

| Method | Source | Notes |
|--------|--------|-------|
| `GetSnapshotAsync` | Calendar + Creative + Apps + Music + Integration catalog + provider label | Aggregated overview |
| `GetProjectAsync` | `CreativeCommand.GetCreativeProject` | Notes preview, quick action **names**, no absolute paths in model text |
| `GetMusicState` | `MusicService` capabilities / current track | Demo catalog honesty |
| `GetProviderStatus` | settings + `isOpenAiKeyConfigured` boolean | Never returns the API key |

## Snapshot contents

- Today / upcoming events (titles + times)
- Creative Projects (id, name, description, notes preview, favorites, last opened, quick action names)
- Recent projects (by `LastOpened`)
- My Apps (id, name, type — not launch paths in formatted model text)
- Music capabilities + current track + demo-catalog note
- Integration domain names
- Provider id / model / configured label

## Security

Context is **read-only**.

Forbidden from this layer:

- `Process.Start` / Shell / PowerShell
- File delete / rewrite
- Arbitrary commands
- WebView operations
- Reading or emitting API keys / OAuth tokens

`FormatForModel` omits absolute filesystem paths and secret material.

## Related

- [ai-assistant.md](ai-assistant.md)
- [assistant-tools.md](assistant-tools.md)
