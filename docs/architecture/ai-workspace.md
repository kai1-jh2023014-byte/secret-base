# AI Workspace

A hub that connects **Creative Projects** to AI tools you already use — **not** an in-app LLM.

In-app chat lives in a separate widget: [Secret Base AI](ai-assistant.md) (`WidgetTypes.Assistant`). Do not merge the two.

- **AI Workspace** = external AI launch hub
- **Secret Base AI** = Personal AI Workspace over Secret Base context/tools

## Purpose

> Project → AI / creation tools, with a safe Command boundary for future natural language.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"ai"` |
| Config | `AiWorkspaceWidgetConfiguration` (`schemaVersion` 1, enabled tool ids) |
| View | `AiWorkspaceView` |
| Shortcut | **Ctrl+Shift+A** / FAB **AI** |

## Built-in tools (MVP)

| Tool | Open behavior |
|------|----------------|
| Cursor | Desktop app via `ICursorLaunchService` (PATH / `%LocalAppData%\Programs\…`) |
| ChatGPT | Official https → system browser |
| Claude | Official https → system browser |
| Gemini | Official https → system browser |

No OAuth, no APIs, no embedded WebView for chat. This widget remains launcher-only even after Secret Base AI finalization.

## Architecture

```
AI Workspace UI / Project Dashboard
        ↓
AiCommand / CreativeCommand.OpenProjectInCursor
        ↓
AiCommandService (+ CreativeProjectService for root)
        ↓
Host
  ├── ICursorLaunchService  → Cursor.exe "<Project.RootFolder>"
  └── TryOpenHttpsUrl       → WebUrlValidator + system browser
```

Cursor discovery (no hardcoded username paths):

1. `PATH` (`cursor.cmd` / `cursor.exe`)
2. `%LocalAppData%\Programs\cursor\Cursor.exe` (and `Cursor` casing)

If Cursor is missing → message + optional **Open Cursor Website** (`https://cursor.com/`). Never auto-install.

## Project Dashboard

Quick Actions include **Open in Cursor** and **Open ChatGPT** for every Project (type does not restrict tools). Cursor requires a registered `RootFolder` that exists on disk.

## Security

Allowed: open known AI app, official websites, registered project root in Cursor.  
Forbidden: arbitrary exe/args, PowerShell, elevation, FS delete/move, Explorer/Taskbar, Host Bridge.

`ICursorLaunchService` accepts **at most one** validated absolute folder argument.

## Explicitly not in MVP

ChatGPT/Claude/Gemini/Cursor APIs, MCP, OAuth, LLM agent, auto code edit, Taskbar/Explorer hooks, auto install.

## Windows manual checklist

1. AI FAB / Ctrl+Shift+A adds widget; Theme applies.
2. ChatGPT / Claude / Gemini open in system browser.
3. Cursor installed → Open launches app; Dashboard **Open in Cursor** opens Project root.
4. Cursor missing → fallback website dialog; no installer.
5. Missing Project root → “Project root not found”.
6. Overlay / other widgets unchanged.
