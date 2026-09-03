# Security Boundaries

## Trust map

| Zone | Code | Trust | May access |
|------|------|-------|------------|
| Trusted Host | App / App.Mac, Core, Infrastructure, Platform.Windows / Platform.Mac | Trusted | Internal services only; Platform still avoids destructive OS APIs |
| Built-in Widget | `SecretBase.Widgets` (Clock, Text, Web chrome, Calendar, Music, Creative, AI Workspace, Secret Base AI, Apps) | Semi-trusted | Own settings/UI; Platform APIs only through approved host services |
| Web Content | Documents inside WebView2 (`WidgetTypes.Web`, Music browse mode) | **Untrusted** | Network rendering only; **no** host object injection to Secret Base APIs |
| Plugin (future) | Marketplace packages | Untrusted | Sandboxed Plugin API only — never Core internals |
| AI (assistant) | LLM providers / registered tools | Restricted | Tool Registry only → existing Commands. No Process, FS, Host Bridge |

## Web Content boundary (Web Widget)

```
Web Content (YouTube, GitHub, …)
        ↓
     Untrusted
        ↓
     WebView2
        ↓
   NO HOST BRIDGE
   · AreHostObjectsAllowed = false
   · IsWebMessageEnabled = false
   · no AddHostObjectToScript / CoreObjects
        ↓
Secret Base Core / Platform / App services
   ← not reachable from the page
```

Web Widget **chrome** (toolbar, theme chrome, URL validation) is Semi-trusted built-in code.
The **document** loaded in WebView2 is always Untrusted.

Forbidden (v0.1 and forward until redesign):

- Injecting Core / Platform objects into the page
- JavaScript → Secret Base arbitrary code / settings
- JavaScript → Win32 / AppKit / filesystem / process / PowerShell / ShellExecute / `/bin/sh`
- Reading arbitrary local files via `file://` (scheme blocked in Core `WebUrlValidator`)
- Injecting secrets into the page

Future “Web → Secret Base API” bridges, if ever added, require an explicit security design and must not ship by accident.

## Calendar Integration Layer

```
CalendarWidgetView (Today agenda)
        ↓
CalendarService (isolates provider failures)
        ↓
ICalendarProvider (+ Capabilities / AuthStatus)
   ├─ Local / Mock (Core)
   ├─ Google ICS (user secret iCal URL — Infrastructure)
   └─ Google Calendar API OAuth read (tokens in Credential Manager via ISecureSecretStore)
```

Common `CalendarEvent` only in Core. No provider DTOs in Core.
**No** Notion Calendar / TimeTree fake providers (see [calendar-integration.md](calendar-integration.md)).
**No** WebView → token injection. Overlay hit-test / DWM remain untouched.

## v0.1 hard rules

1. Do not implement file delete/move, process kill, registry shell edits, or admin elevation.
2. Do not inject JS bridges from WebView into host APIs.
3. Do not give AI unrestricted OS control. Secret Base AI may run **registered tools only**.
4. Logging must avoid secrets and unnecessary personal data.
5. Web Widget URL gate allows only `http` / `https` (see [web-widget.md](web-widget.md)).
6. Custom Apps: registration never grants Shell, elevation, or command-line arguments.

## User confirmation (Secret Base AI)

Confirmation required before:

- Opening a Creative Project / Cursor folder
- Opening Classroom / Calendar integrations
- Launching a My Apps target
- Playing a music track

Not required for read-only calendar / project / app lists.

API keys stay in Credential Manager (`ISecureSecretStore`). Never log secret values.

## Safe Mode / Reset (designed, not fully implemented)

Planned recovery switches:

- Safe Mode (minimal UI, skip custom layout)
- Reset configuration
- Exit Secret Base (already implemented as process exit)
