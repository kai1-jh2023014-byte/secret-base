# Security Boundaries

## Trust map

| Zone | Code | Trust | May access |
|------|------|-------|------------|
| Trusted Host | App, Core, Infrastructure, Platform | Trusted | Internal services only; Platform still avoids destructive OS APIs |
| Built-in Widget | `SecretBase.Widgets` | Semi-trusted | Own settings/UI; Platform APIs only through approved host services |
| Web Content | WebView2 documents | Untrusted | Network rendering only; **no** host object injection to Secret Base APIs |
| Plugin (future) | Marketplace packages | Untrusted | Sandboxed Plugin API only — never Core internals |
| AI (future) | LLM providers / tools | Restricted | Observation → Safe → Confirm → Restricted ladder |

## v0.1 hard rules

1. Do not implement file delete/move, process kill, registry shell edits, or admin elevation.
2. Do not inject JS bridges from WebView into host APIs.
3. Do not give AI (when added) unrestricted OS control.
4. Logging must avoid secrets and unnecessary personal data.

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
