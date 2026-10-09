# Integration Hub (v0.2)

Secret Base v0.2 connects **existing widgets, external services, and user-registered apps** behind a Command catalog. It does **not** add a new Classroom Widget, LLM, plugin host, or Overlay changes.

## Audit note

This repository has **no Classroom Widget** (no type, view, or Google Classroom API client). Classroom is integrated by:

1. Add Widget catalog entry **Classroom** → existing `WidgetTypes.Web` preset at `https://classroom.google.com/`
2. `ClassroomCommand` (`Open` / `Refresh` / `GetAssignments` / `GetCourses`)
3. `GetAssignments` / `GetCourses` fail honestly until a real provider exists — **no invented assignments**

## Layers

```
UI / Secret Base AI
        ↓
IntegrationCatalog (metadata) + IntegrationCommandService (router)
        ↓
Existing Commands (CalendarCommand, MusicCommand, CreativeCommand,
                   AiCommand, ClassroomCommand, AppCommand)
        ↓
Services / providers
        ↓
Host → ITargetLaunchService / ICursorLaunchService / browser
```

AI never calls Shell, PowerShell, arbitrary Process, filesystem, Win32, Registry, or WebView Host Bridge.

## Custom apps vs Creative Projects

| | Creative Project | Custom App |
|--|------------------|------------|
| Meaning | A making-of record (name, notes, root, resources) | Something you launch |
| Example | “Pokemon Calculator” project | `PokemonCalc.exe` |
| Link | Optional `CustomApp.CreativeProjectId` / `ProjectRoot` | Open in Cursor uses root only |

Registration is **not** admin, Shell, or plugin privilege.

## Persistence

`%LocalAppData%\SecretBase\apps\apps.json`

- `schemaVersion` **1**
- Atomic write (`.tmp` → copy → delete)
- Invalid targets dropped; missing files on disk are kept (host reports “not found” on launch)

Full My Apps field reference, Japanese guide, and connection formats: [my-apps.md](my-apps.md).

## Add Widget groups

Information (Clock, Text, Web, Calendar, Classroom, Progress) · Creative (Projects, Music) · AI (AI Workspace, Secret Base AI) · Apps (My Apps) · Focus (Pomodoro)

## Out of scope (Integration Hub slice)

Classroom API OAuth, Spotify APIs, Notion/TimeTree fakes, plugin marketplace, Overlay/Explorer/Taskbar.

Secret Base AI is a **separate** adapter layer on these Commands — see [ai-assistant.md](ai-assistant.md).
