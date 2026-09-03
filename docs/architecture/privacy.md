# Privacy

Secret Base observes a little, remembers less, and never spies.

| Bucket | What |
|--------|------|
| **Observed** | Foreground process name, idle vs active, Secret Base clicks, registered project/app names, calendar titles, todo titles, workspace titles, session summaries. |
| **Remembered** | Scoped memory you or Safe Auto write (session, project, workflow, decision, preference, idea, note). Importance, source, expiry. Ranked recall — never a full dump. |
| **Allowed (Safe Auto)** | Prepare a workspace, start a local focus timer, remember a short fact, suggest cleanup candidates, show a quiet continuation card. |
| **Requires confirmation** | Open a registered project or app, continue a workspace into an app, calendar writes, music play, unregister / return a Block item. |
| **Never collected** | Keystrokes, clipboard, passwords, API keys, tokens, browser page contents, microphone, camera, screenshots, arbitrary document bodies, unrestricted filesystem paths. |
| **Never automatic** | Arbitrary shell, PowerShell, arbitrary executables, OS file delete, silent app launch, learning that turns Confirmation into Auto Action. |

Browser processes are observable. Specific page contents are not.

Users can inspect and forget memories, disable automation rules, and set quiet hours (default 22:00–08:00).

Implementation: `PrivacyManifest`, Memory UI, Privacy Center, `ObservationSanitizer`.
