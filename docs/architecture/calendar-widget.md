# Calendar Widget & Integration Layer

Think / Manage layer: keep the schedule visible on the Secret Base Desktop.

See also: [calendar-integration.md](calendar-integration.md) (provider investigation + hub design).

## UX

Month calendar + day agenda (not browser Chrome):

```
‹  October 2026  ›                    [Notify]
S  M  T  W  T  F  S
… month grid with event dots …

Today · Friday, Oct 10
┃ 09:00  〜  12:00
  Study
…

[Add one] [Bulk add]
title · start · end · Add
— or paste —
19時勉強
22:00-23:00 英語
[Add all]

Local · Google
[Refresh] [Open] [Connect]
```

- Day cells show accent dots for days with events; select a day to see its agenda.
- Each event shows **start 〜 end** (or All day).
- **Notify** (on by default): when a timed event starts, an in-widget banner appears and the host status line updates.
- **Bulk add**: paste several lines / JP phrases; events apply to the **selected day**.
- Add via **Cal** FAB / **Ctrl+Shift+C**. Optional Mock provider checkbox. Default layout still seeds only Clock + Text.

## Architecture

```
CalendarWidgetView
        ↓
CalendarService (merge + per-provider status + no crash on failure)
        ↓
ICalendarProvider (+ Capabilities / AuthStatus)
   ├─ LocalCalendarProvider      (Core — store-backed)
   ├─ MockCalendarProvider       (Core — deterministic demo)
   ├─ GoogleCalendarIcsProvider  (Infrastructure — secret ICS HTTPS URL)
   └─ GoogleCalendarApiProvider  (Infrastructure — OAuth read/write; tokens in Credential Manager)

CalendarMonthLayout / CalendarBulkEntryParser / CalendarReminderMonitor  (pure Core)
```

Common model: `CalendarEvent` (+ Color / Source / LastUpdated).
Provider-specific DTOs never enter Core. Core never references Google SDKs.

**Not shipped as providers:** Notion Calendar (no third-party sync API), TimeTree (Connect API terminated) — use Web Widget / browser. See calendar-integration.md.

## Google

1. **ICS (zero OAuth):** paste secret iCal URL into Add Calendar dialog.
2. **API read/write (OAuth):** place `%LocalAppData%\SecretBase\credentials\google-oauth-client.json`, then **Connect** in the widget. Refresh token → Windows Credential Manager (`SecretBase/Calendar/Google/RefreshToken`). Scope: `calendar.events`.

## Local Today / month UX

- **Add** / **Bulk add** write to Secret Base local storage only (not Google).
- Start and end times are editable (`HH:mm`).
- Local events show **×** to remove (registration only — never deletes Google or disk files).
- Agenda auto-refreshes about every 20s so Base AI local writes appear without a manual Refresh.
- Widget config flags: `NotifyOnEventStart`, `NotifyLeadMinutes`.

## Base AI → Calendar widget

`calendar_add_event` defaults to **local**. Pass `events:[{title,hour,...}]` for a full day plan in one confirmation. Use `destination=google` only when asking for Google. `calendar_remember_usual` is only for 「いつも」 recurring slots.

Japanese timed lists such as `19時勉強、20時食事、22時半ギターをいれて` and colon ranges `22:00から23:00まで英語` are parsed **locally** (no remote model round) and go straight to Confirm → Run. Pomodoro phrases (including typos like `ぽもどーとタイマー`) start the timer locally too. When a remote model still fails, the error names the provider, model, timeout seconds, and what to check.

## Security

- Overlay / HWND / DWM / SetWindowRgn — **unchanged**
- No tokens in layout JSON / logs / git
- No WebView host bridge for calendar
- Agenda cache JSON stores events only
- Reminders are in-process notices (no OS shell injection)

## Persistence (widget configuration)

Flags: `GoogleIcsUrl`, `IncludeMockProvider`, `EnableGoogleApiProvider`, `GoogleOAuthClientConfigPath`, `NotifyOnEventStart`, `NotifyLeadMinutes`, local `Events` (Color/Source included). Never tokens.
