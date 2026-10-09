# Calendar Widget & Integration Layer

Think / Manage layer: keep today's schedule visible on the Secret Base Desktop.

See also: [calendar-integration.md](calendar-integration.md) (provider investigation + hub design).

## UX (MVP)

Today agenda — not a full month grid:

```
Today
● 09:00  〜 12:00
  Study
…
Google Calendar · School · Local
[Refresh] [Open] [Connect]
```

Add via **Cal** FAB / **Ctrl+Shift+C**. Optional Mock provider checkbox. Default layout still seeds only Clock + Text.

## Architecture

```
CalendarWidgetView
        ↓
CalendarService (merge + per-provider status + no crash on failure)
        ↓
ICalendarProvider (+ Capabilities / AuthStatus)
   ├─ LocalCalendarProvider      (Core — config / sample agenda)
   ├─ MockCalendarProvider       (Core — deterministic demo)
   ├─ GoogleCalendarIcsProvider  (Infrastructure — secret ICS HTTPS URL)
   └─ GoogleCalendarApiProvider  (Infrastructure — OAuth read; tokens in Credential Manager)
```

Common model: `CalendarEvent` (+ Color / Source / LastUpdated).
Provider-specific DTOs never enter Core. Core never references Google SDKs.

**Not shipped as providers:** Notion Calendar (no third-party sync API), TimeTree (Connect API terminated) — use Web Widget / browser. See calendar-integration.md.

## Google

1. **ICS (zero OAuth):** paste secret iCal URL into Add Calendar dialog.
2. **API read/write (OAuth):** place `%LocalAppData%\SecretBase\credentials\google-oauth-client.json`, then **Connect** in the widget. Refresh token → Windows Credential Manager (`SecretBase/Calendar/Google/RefreshToken`). Scope: `calendar.events`.

## Local Today widget UX

- **Add** writes to Secret Base local storage only (not Google).
- Time field is editable (`HH:mm`); use **− / +** to nudge by 15 minutes.
- Local events show **×** to remove (registration only — never deletes Google or disk files).
- Agenda auto-refreshes about every 20s so Base AI local writes appear without a manual Refresh.

## Base AI → Today widget

`calendar_add_event` defaults to **local** (Today widget). Pass `events:[{title,hour,...}]` for a full day plan in one confirmation. Use `destination=google` only when asking for Google. `calendar_remember_usual` is only for 「いつも」 recurring slots.

Japanese timed lists such as `19時勉強、20時食事、22時半ギターをいれて` and colon ranges `22:00から23:00まで英語` are parsed **locally** (no remote model round) and go straight to Confirm → Run. Pomodoro phrases (including typos like `ぽもどーとタイマー`) start the timer locally too. When a remote model still fails, the error names the provider, model, timeout seconds, and what to check.

## Security

- Overlay / HWND / DWM / SetWindowRgn — **unchanged**
- No tokens in layout JSON / logs / git
- No WebView host bridge for calendar
- Agenda cache JSON stores events only

## Persistence (widget configuration)

Flags: `GoogleIcsUrl`, `IncludeMockProvider`, `EnableGoogleApiProvider`, `GoogleOAuthClientConfigPath`, local `Events` (Color/Source included). Never tokens.
