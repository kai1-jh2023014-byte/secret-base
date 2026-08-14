# Calendar Widget & Integration Layer

Think / Manage layer: keep today's schedule visible on the Secret Base Desktop.

## UX (v0.1)

Today agenda — not a full month grid:

```
Today
09:00  〜 12:00
東進
14:00  〜 16:00
Programming
…
Google Calendar / Local
[Refresh] [Open Calendar]
```

Add via **Cal** FAB / **Ctrl+Shift+C**. Default layout still seeds only Clock + Text.

## Architecture

```
CalendarWidgetView
        ↓
CalendarService
        ↓
ICalendarProvider
   ├─ LocalCalendarProvider      (Core — config / sample agenda)
   ├─ GoogleCalendarIcsProvider  (Infrastructure — secret ICS HTTPS URL)
   └─ Notion / others (future)
```

Common model: `CalendarEvent` (Id, Provider, CalendarId/Name, Title, Start, End, IsAllDay, Location, Description, Url).
Provider-specific DTOs never enter Core.

## Google Calendar (no OAuth client in app)

User pastes Google Calendar → **Settings → Integrate calendar → Secret address in iCal format**.
`GoogleCalendarIcsProvider` fetches ICS over HTTPS and maps VEVENT → `CalendarEvent` via Core `IcsCalendarParser`.

Without an ICS URL, a local Creative-day sample agenda (東進 / Programming / Guitar) is shown so the widget is useful immediately.

**Open Calendar** launches `https://calendar.google.com/` (https only).

## Security

- Overlay / HWND / DWM / SetWindowRgn — **unchanged** (PR #8 verified)
- No Google OAuth client secrets in the binary
- ICS URL is user-provided; treat as sensitive (stored in layout JSON under AppData)
- Future Notion/OAuth providers require explicit consent UX + Platform/Infrastructure isolation

## Persistence

```json
{
  "type": "calendar",
  "configuration": {
    "GoogleIcsUrl": "https://calendar.google.com/calendar/ical/…/basic.ics",
    "OpenCalendarUrl": "https://calendar.google.com/",
    "UseSampleAgendaWhenEmpty": false,
    "Events": [
      {
        "Id": "…",
        "Provider": "local",
        "Title": "Programming",
        "Start": "2026-08-13T14:00:00+09:00",
        "End": "2026-08-13T16:00:00+09:00",
        "IsAllDay": false
      }
    ]
  }
}
```

Legacy Date-only events still load as all-day.

## Windows manual checklist

```powershell
git checkout cursor/calendar-widget-6d90
git pull
.\run.ps1
```

1. Overlay click-through / edges / Exit / layout restore still OK (do not regress PR #8).
2. Clock / Text / Blocks / Web unchanged.
3. **Cal** → Today agenda with sample events (or ICS events after paste).
4. Refresh / Open Calendar work.
5. Restart restores configuration.
