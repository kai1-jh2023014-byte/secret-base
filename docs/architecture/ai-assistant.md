# Secret Base AI (Assistant)

In-app chat that **calls existing Commands**. Not a ChatGPT clone, and not a replacement for [AI Workspace](ai-workspace.md) (the Cursor / ChatGPT / Claude / Gemini launcher).

## Purpose

```
User
  ↓
Secret Base AI widget
  ↓
IAssistantService
  ↓
IAiProvider (LLM)
  ↓
IAiToolRegistry / IAiToolExecutor
  ↓
Existing Commands (Calendar / Creative / Ai / Integration / App / Music)
  ↓
Existing Services
  ↓
Host → ITargetLaunchService / ICursorLaunchService / browser
```

The LLM never calls `Process.Start`, PowerShell, the filesystem, Host Bridge, or WebView2.

## Widget

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"assistant"` |
| Label | **Secret Base AI** (Add Widget → AI group) |
| Config | `AssistantWidgetConfiguration` (`schemaVersion` 1, no secrets) |
| View | `AssistantWidgetView` |
| Default layout | **Not seeded** — add from catalog |

AI Workspace (`WidgetTypes.Ai`) stays the launcher hub.

## Provider boundary

`IAiProvider.ChatAsync(messages, tools, model)` lives in Core. SDKs stay out of Core.

| Id | MVP |
|----|-----|
| `openai` | Real HTTP Chat Completions (`OpenAiAssistantProvider`) |
| `gemini` | Stub → unavailable (swap later) |
| `local` | Stub → unavailable (Ollama later) |

Settings (`%LocalAppData%\SecretBase\settings\assistant.json`): **providerId + model only**.

API key: `ISecureSecretStore` / Windows Credential Manager (`SecretBase/Assistant/OpenAI/ApiKey`). Never Git, layout JSON, widget configuration, or logs.

## Tools (MVP)

OpenAI function names cannot contain `.`, so registry names use underscores. Each tool is an adapter over an existing Command.

| Tool | Command | Confirm |
|------|---------|---------|
| `calendar_get_today` | `CalendarCommand.GetTodayEvents` | no |
| `calendar_get_upcoming` | `CalendarCommand.GetUpcoming` | no |
| `creative_list_projects` | `CreativeCommand.SearchProjects` | no |
| `creative_open_project` | `CreativeCommand.OpenCreativeProject` | yes |
| `cursor_open_project` | `AiCommand.OpenProjectInCursor` | yes |
| `integration_open` | `IntegrationCommandService` (`classroom` / `calendar`) | yes |
| `apps_list` | `AppCommand.ListApps` | no |
| `apps_open` | `AppCommand.OpenApp` | yes |
| `music_search` | `MusicCommand.SearchTrack` if Search capability exists | no |
| `music_play` | `MusicCommand.PlayTrackById` if Playback capability exists | yes |

Unknown names (including `shell.run`) are rejected. Arguments that look like paths, URLs, or command lines are rejected.

Music must not invent Spotify/YouTube APIs. Demo catalog search/play is labeled as demo catalog.

## Confirmation

Observation tools run immediately. Launch tools pause with:

> Open project '…' in Cursor?

**[Cancel] [Run]**

Host performs launch only after confirm + successful Command result (`ShouldLaunch` / `ShouldOpenCursorAtFolder`).

## Context

The model receives a short system prompt plus **tool metadata**. It does not receive the whole PC, OAuth tokens, or API keys. History is the current widget session, capped (`AssistantService.MaxVisibleMessages`).

## Errors (shown in UI)

| Case | Copy |
|------|------|
| No API key | `AI is not configured.` `Open AI Settings.` |
| Provider down | `AI service is unavailable. Please try again.` |
| Unknown / blocked tool | `This action is currently unavailable.` |
| Cursor launch failed | `Cursor could not be opened.` |

Failures are never rewritten as success.

## Security

Forbidden: arbitrary shell / PowerShell / process args, file delete/rewrite, Host Bridge, WebView2 injection, logging or git-storing API keys.

Allowed: registered tools only, then the same Platform launch path humans already use.

## Out of scope (v0.3+)

Autonomous agents, long-term memory, Computer Use, arbitrary file/shell, vision, voice, browser automation, plugin system, Gemini/Ollama HTTP implementations.
