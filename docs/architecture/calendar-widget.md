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
2. **API read (OAuth):** place `%LocalAppData%\SecretBase\credentials\google-oauth-client.json`, then **Connect** in the widget. Refresh token → Windows Credential Manager (`SecretBase/Calendar/Google/RefreshToken`). Scope: `calendar.readonly`.

## Security

- Overlay / HWND / DWM / SetWindowRgn — **unchanged**
- No tokens in layout JSON / logs / git
- No WebView host bridge for calendar
- Agenda cache JSON stores events only

## Persistence (widget configuration)

Flags: `GoogleIcsUrl`, `IncludeMockProvider`, `EnableGoogleApiProvider`, `GoogleOAuthClientConfigPath`, local `Events` (Color/Source included). Never tokens.
