# Calendar Widget & Integration Layer

Local month calendar on the Secret Base Desktop. v0.1 is **local-only** —
no Outlook / Google cloud credentials or network sync.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"calendar"` |
| Configuration | `CalendarWidgetConfiguration` |
| View | `SecretBase.Widgets.Calendar.CalendarWidgetView` |

## How to add

- Always-visible **Cal** FAB
- Debug chrome **Add Cal**
- **Ctrl+Shift+C**

Default layout still seeds only Clock + Text. Calendar is never auto-inserted.
Does **not** change Desktop Overlay hit-testing / DWM (PR #8 foundation).

## Layers

| Layer | Owns |
|-------|------|
| **Core / Calendar** | `ICalendarEventSource`, `CalendarEvent`, `CalendarMonthBuilder`, `LocalCalendarEventSource`, `EmptyCalendarEventSource` |
| **Core / Widgets** | `CalendarWidgetConfiguration` (FirstDayOfWeek, pinned month, local Events bag) |
| **Widgets** | Month grid UI (prev/next/today), theme tokens |
| **App** | `DesktopPage` wiring + Add dialog; `WidgetFrame` move/resize |
| **Infrastructure** | Existing `JsonLayoutStore` (opaque configuration) |

```
CalendarWidgetView
        ↓
CalendarMonthBuilder (pure)
        ↓
ICalendarEventSource
   ├─ EmptyCalendarEventSource
   └─ LocalCalendarEventSource  ← v0.1 (from widget configuration)
   └─ (future) Outlook / Google providers in Platform/Infrastructure
```

## Security

- Local-only display + optional local events in layout JSON
- No cloud OAuth, no calendar REST APIs in v0.1
- Future providers must implement `ICalendarEventSource` behind Platform/Infrastructure and require explicit user consent (see security-boundaries.md)

## Persistence example

```json
{
  "type": "calendar",
  "configuration": {
    "FirstDayOfWeek": "Monday",
    "FollowToday": true,
    "PinnedYear": 0,
    "PinnedMonth": 1,
    "Events": [
      { "Id": "...", "Date": "2026-08-13", "Title": "Ship Calendar", "Notes": null }
    ]
  }
}
```

## Windows manual checklist

```powershell
git checkout cursor/calendar-widget-6d90
git pull
.\run.ps1
```

1. Clock / Text / Blocks / Web still work; Desktop click-through (PR #8) unchanged.
2. **Cal** → create calendar → month grid shows; Today highlighted.
3. Prev / Next / Today navigate; restart restores pinned month when FollowToday is false.
4. Optional first event appears as a day dot + summary.
5. Theme **Aa** tints calendar chrome.
6. Drag via WidgetFrame grip only (not from day cells for move).
