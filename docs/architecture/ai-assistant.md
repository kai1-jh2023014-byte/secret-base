# Secret Base AI (Assistant)

In-app assistant that **calls existing Commands**. Not a ChatGPT clone, and not a replacement for [AI Workspace](ai-workspace.md) (the Cursor / ChatGPT / Claude / Gemini launcher).

v0.3 adds a **read-only Context Layer** and clearer suggest-vs-execute boundaries. Still **not** an autonomous agent.

## Architecture

```
User
 ↓
Assistant Widget
 ↓
AssistantService
 ↓
Context / Tool Registry
 ↓
Command
 ↓
Service
 ↓
Host (ITargetLaunchService / ICursorLaunchService / browser)
```

The LLM never calls `Process.Start`, PowerShell, the filesystem, Host Bridge, or WebView2.

See also: [assistant-context.md](assistant-context.md) · [assistant-tools.md](assistant-tools.md)

## Widget

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"assistant"` |
| Label | **Secret Base AI** (Add Widget → AI group) |
| Config | `AssistantWidgetConfiguration` (`schemaVersion` 1, no secrets) |
| View | `AssistantWidgetView` — chat, activities (`Calendar ✓`), confirmation, provider status |
| Default layout | **Not seeded** |

AI Workspace (`WidgetTypes.Ai`) stays the launcher hub.

## Provider boundary

`IAiProvider.ChatAsync(messages, tools, model)` lives in Core. SDKs stay out of Core.

| Id | Status |
|----|--------|
| `openai` | Real HTTP Chat Completions |
| `gemini` / `local` | Stubs → unavailable (swap later) |

API key: Credential Manager only (`ISecureSecretStore`). Never Git, layout JSON, widget config, logs, projects.json, apps.json, or conversation history.

## Suggest vs execute

| Mode | Behavior |
|------|----------|
| **Answer** | Read-only tools + explanation |
| **Suggest** | May recommend opening Cursor/project/app without calling launch tools |
| **RequestConfirmation** | Launch tool selected → Cancel / Run |
| **Execute** | After Run → Command → Host launch |

Launch tools are **not** auto-run. System prompt forbids calling them unless the user clearly asked to open/launch/play.

## Conversation history

Session-only (in-memory), capped (`MaxVisibleMessages = 20`). Supports “さっきのプロジェクト” follow-ups. No long-term memory. No secrets in history.

## Errors (never faked as success)

| Case | Copy |
|------|------|
| No API key | `AI is not configured.` `Open AI Settings.` |
| Provider down | `AI service is unavailable. Please try again.` |
| Unknown / blocked tool | `This action is currently unavailable.` |
| Cursor launch failed | `Cursor could not be opened.` |

## What AI can / cannot do

**Can:** read calendar, projects, apps, music state; suggest next steps; request confirmation to open Cursor / apps / integrations / play demo catalog tracks.

**Cannot:** arbitrary shell/PowerShell, file delete/rewrite, Host Bridge, WebView2 control, Computer Use, autonomous multi-step agents, invent Spotify/Classroom APIs, invent unregistered projects.

## Out of scope (later)

Gemini/Ollama HTTP, autonomous agents, long-term memory/RAG, MCP, plugins, Computer Use, vision/voice.
