# Security Boundaries

## Trust map

| Zone | Code | Trust | May access |
|------|------|-------|------------|
| Trusted Host | App, Core, Infrastructure, Platform | Trusted | Internal services only; Platform still avoids destructive OS APIs |
| Built-in Widget | `SecretBase.Widgets` (Clock, Text, Web chrome) | Semi-trusted | Own settings/UI; Platform APIs only through approved host services |
| Web Content | Documents inside WebView2 (`WidgetTypes.Web`) | **Untrusted** | Network rendering only; **no** host object injection to Secret Base APIs |
| Plugin (future) | Marketplace packages | Untrusted | Sandboxed Plugin API only — never Core internals |
| AI (future) | LLM providers / tools | Restricted | Observation → Safe → Confirm → Restricted ladder |

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
- JavaScript → Win32 / filesystem / process / PowerShell / ShellExecute
- Reading arbitrary local files via `file://` (scheme blocked in Core `WebUrlValidator`)
- Injecting secrets into the page

Future “Web → Secret Base API” bridges, if ever added, require an explicit security design and must not ship by accident.

## v0.1 hard rules

1. Do not implement file delete/move, process kill, registry shell edits, or admin elevation.
2. Do not inject JS bridges from WebView into host APIs.
3. Do not give AI (when added) unrestricted OS control.
4. Logging must avoid secrets and unnecessary personal data.
5. Web Widget URL gate allows only `http` / `https` (see [web-widget.md](web-widget.md)).

## User confirmation (future)

Confirmation required before:

- Destructive file operations
- Bulk automation
- Privilege escalation
- Sending local content to cloud providers
- Installing plugins

## Safe Mode / Reset (designed, not fully implemented)

Planned recovery switches:

- Safe Mode (minimal UI, skip custom layout)
- Reset configuration
- Exit Secret Base (already implemented as process exit)
