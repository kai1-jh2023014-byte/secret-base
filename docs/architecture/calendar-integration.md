# Calendar Hub / Integration Layer

Date: 2026-08-14  
Branch: `cursor/calendar-hub-6d90`  
Principle: multi-provider hub first; only official / user-consented integrations; no Overlay redesign.

## Investigation summary (official sources)

### Google Calendar — **official API available (preferred)**

| Item | Finding |
|------|---------|
| API | [Google Calendar API v3](https://developers.google.com/workspace/calendar/api) |
| Auth | OAuth 2.0 installed-app / loopback + PKCE |
| Read scopes (minimal) | `https://www.googleapis.com/auth/calendar.readonly` and/or `…/calendar.events.readonly` |
| MVP ops | calendarList + events.list (read only) |
| Writes | Deferred until read path is stable |

**Safety:** Client ID/secret never committed. Refresh tokens in Windows Credential Manager (not plain JSON). No token logging. ICS secret-URL path remains as a zero-OAuth fallback.

### Notion Calendar vs Notion API — **do not conflate**

| Path | Official? | Secret Base stance |
|------|-----------|--------------------|
| **Notion Calendar app** direct event CRUD API | **No** dedicated Calendar API for third parties. Help docs expose a local deep-link (`cron://…`) to *open* an event in the Notion Calendar desktop app — not a sync API. | Do **not** ship a fake “Notion Calendar Provider”. |
| **Notion Data API** (databases / pages with `date` properties) | Yes — [Notion API](https://developers.notion.com/). Calendar *views* are UI over databases. | Future optional **Notion data** provider (database with date property), labeled clearly as “Notion data”, not “Notion Calendar”. Not in this MVP. |

### TimeTree — **third-party Connect API terminated**

Official notice (developers.timetreeapp.com, Dec 2023): **Connect App / third-party API ended 2023-12-22**. Remaining integrations called out are limited (e.g. Alexa). Community scrapers / reverse-engineered endpoints are **out of scope**.

| Allowed | Forbidden |
|---------|-----------|
| Open TimeTree in **Web Widget** / browser | Unofficial private API, scraping, cookie theft |
| User exports / manual ICS if TimeTree ever provides one | Shipping a TimeTreeProvider that pretends official sync |

### Windows credential storage

Documented Win32: `CredWrite` / `CredRead` / `CredDelete` (advapi32) — store OAuth refresh tokens under `SecretBase/Calendar/*` targets. Isolated in `Platform.Windows` behind `ISecureSecretStore`.

## Target architecture

```
Calendar Widget (Today agenda)
        ↓
CalendarService (merge + per-provider status)
        ↓
ICalendarProvider (+ Capabilities / AuthStatus)
   ├─ Local / Mock          (Core)
   ├─ Google ICS            (Infrastructure — user secret iCal URL)
   ├─ Google Calendar API   (Infrastructure — OAuth read; tokens in Credential Manager)
   ├─ Notion data           (future — official Notion API only)
   └─ TimeTree              (not a provider — Web Widget / browser)
```

Core never references Google/Notion SDKs.

## Capabilities model

Not every provider supports CRUD. `CalendarProviderCapabilities` flags:

- `ReadEvents`, `ListCalendars`, `Authentication`
- `CreateEvents`, `UpdateEvents`, `DeleteEvents` (reserved; Google write deferred)

## Security boundaries

```
Web Content (Untrusted)  ≠  Calendar Provider (Semi-trusted host code)
        ↓                            ↓
   no host bridge            External HTTPS API + OAuth
```

- No WebView → Google token injection
- No tokens in logs / git / layout JSON
- Provider failures stay inside the widget (“Google Calendar unavailable”)

## MVP shipped in this milestone

1. Enriched Core hub (`Capabilities`, `AuthStatus`, `CalendarInfo`, event `Color`/`Source`)
2. `MockCalendarProvider` for deterministic UI/tests
3. Agenda cache JSON (events only — never tokens)
4. Google OAuth **read** provider (configured only when user supplies client file under AppData)
5. Existing Local + Google ICS retained
6. Docs: Notion/TimeTree honest limits; Overlay untouched

## Manual Google OAuth setup (Windows)

1. Google Cloud Console → OAuth client (Desktop)  
2. Save `%LocalAppData%\SecretBase\credentials\google-oauth-client.json`  
   (`client_id`, optional `client_secret` for desktop clients — **never commit**)  
3. Calendar Widget → Connect Google (when UI exposes it) / or provider auto-detects file  
4. Browser consent → loopback PKCE → refresh token in Credential Manager  

## Out of scope (explicit)

- Notion Calendar Provider (no official sync API)
- TimeTree Provider (Connect API terminated)
- Google event write/delete
- Overlay / DWM / SetWindowRgn changes
- SQLite (JSON cache only for now)
