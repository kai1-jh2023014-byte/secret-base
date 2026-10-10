# Secret Base AI (Assistant)

In-app **Personal AI Workspace** that understands Secret Base (Calendar, Creative, Apps, Music, Integrations) and runs **existing Commands** after confirmation. Not a ChatGPT clone, and not a replacement for [AI Workspace](ai-workspace.md) (Cursor / ChatGPT / Claude / Gemini launcher).

**v0.4** completes the AI MVP loop:

```
Context → Plan → Confirmation → Action → Result
```

Still **not** an autonomous agent. No infinite loops, Computer Use, shell, or Host Bridge.

## Architecture

```
User
 ↓
Assistant Widget
 ↓
Assistant Service
 ├─ Local Fast Path (clear reads / Safe Auto / confirms — no Jev / no LLM)
 ├─ Context
 ├─ Planner
 ├─ Jev (situational judgment + Action/Suggestion gate — not a chat provider)
 ├─ Tool Registry
 └─ Conversation (Gemini / OpenAI / Local) — complex inference only
 ↓
Command
 ↓
Service
 ↓
Host (ITargetLaunchService / ICursorLaunchService / browser)
```

The LLM never calls `Process.Start`, PowerShell, the filesystem, Host Bridge, or WebView2.

**Routing:** clear agenda / project list / todo list / music pause / timed schedule writes complete locally. Jev decides situation / next_step / gate when the request is situational or an Action/Suggestion still needs the model. Gemini (and other conversation providers) are for ambiguous or multi-step inference — not for “今日の予定を見せて”. See [jev-decision.md](jev-decision.md). `AssistantTurnResult.RouteTrace` records route id, local completion, and whether Jev / the conversation model ran (no secrets).

See also:

- [assistant-context.md](assistant-context.md)
- [assistant-tools.md](assistant-tools.md)
- [ai-planning.md](ai-planning.md)
- [ai-security.md](ai-security.md)

## Widget

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"assistant"` |
| Label | **Secret Base AI** (Add Widget → AI group) |
| Config | `AssistantWidgetConfiguration` (`schemaVersion` 1, no secrets) |
| View | `AssistantWidgetView` — bubbles, Plan, activities, multi-action Confirm, Results, Settings, onboarding |
| Default layout | **Not seeded** |

**AI Workspace** (`WidgetTypes.Ai`) stays the external-AI launcher. **Secret Base AI** understands and operates Secret Base.

## Provider boundary

`IAiProvider.ChatAsync(messages, tools, model)` lives in Core. SDKs stay out of Core.

| Id | Status |
|----|--------|
| `openai` | Real HTTP Chat Completions |
| `gemini` / `local` | Stubs → **AI provider is unavailable.** (swappable later) |

API key: Credential Manager only (`ISecureSecretStore`). Never Git, layout JSON, widget config, logs, projects.json, apps.json, or conversation history. Settings UI shows `••••••••`, never the raw key.

Settings (`assistant.json` schema **2**): provider, model, `maxSteps` (default 5, hard cap 8), `requireConfirmationForActions`.

## What Secret Base AI can do

- Read scoped context (calendar, free time, projects, apps, music capabilities, integrations, provider status)
- Build a thin **Plan** for known workflows (today priority, start project)
- Run **Suggest** tools (`schedule_recommend`, `project_recommend`, `music_recommend`) without launching Host
- Propose **RequiresConfirmation** actions (Cursor, open project, apps, integrations, music play)
- After **Run**, execute via existing Commands and report honest success/failure
- Keep short session memory (e.g. resolve “さっきの”)

## What Secret Base AI cannot do

- Arbitrary shell / PowerShell / cmd
- Arbitrary process or file delete/rewrite
- Registry, elevation, mouse/keyboard automation, Computer Use
- Unrestricted browser automation / Host Bridge / WebView2 control
- Infinite agent loops (max steps enforced)
- Long-term memory / RAG / voice / vision
- Invent Spotify, Classroom OAuth, or unregistered projects
- Claim demo-catalog music is Spotify/YouTube API playback

## Confirmation-required operations

| Tool | After Run |
|------|-----------|
| `creative_open_project` | Creative dashboard |
| `cursor_open_project` | Host `ICursorLaunchService` |
| `apps_open` | Host launch / browser |
| `integration_open` | Browser / Web Widget |
| `music_play` | Demo/local playback only if capability exists |

Multi-action confirms list each step; risky Host launches are called out.

## Conversation history

Session-only (in-memory), capped (`MaxVisibleMessages = 20`). Secrets rejected/stripped. No long-term memory.

## Errors (never faked as success)

| Case | Copy |
|------|------|
| No API key | `AI API key is not configured.` `Open AI Settings.` (OpenAI and Gemini keys are stored in separate Credential Manager / Keychain slots.) |
| Provider stub / down | `AI provider is unavailable.` |
| Timeout | `AI response timed out. Provider: … (id), model: …. No … within 60s. Check network / API key…` (includes provider, model, phase, next steps) |
| Network | `Could not connect… Provider: …` + connectivity hint |
| Unavailable | `AI provider is unavailable. Provider: …` + detail when known |
| Calendar/tool failure | Honest domain message |
| Cursor launch failed | `Cursor could not be opened.` |
| Max steps | `Stopped after the maximum number of steps.` |

## Example UX

1. User: 「今日何をやればいい？」 → Calendar + Projects → candidates (not life assertions)
2. User: 「じゃあPokemonを始めよう」 → Plan → Confirm Cursor open → Run → Host launches → 「開きました」

## Related

- Decision: v0.4 Personal AI Workspace (see decision log)
- Roadmap: AI feature line complete at v0.4; further agent work is out of scope until requested
