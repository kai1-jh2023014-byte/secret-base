# Music Widget (native UX)

Music is a first-class Creative OS surface — **Secret Base UI first**, external services as Providers behind Commands.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"music"` |
| Configuration | `MusicWidgetConfiguration` (`Sources`, `ActiveSourceId`, `CurrentTrack`) |
| View | `MusicWidgetView` (search / current track / transport) |
| Commands | `MusicCommand` → `MusicCommandService` → `IMusicProvider` |

## How to add

- **♪** FAB / debug **Add Music** / **Ctrl+Shift+M**

## Web Widget vs Music Widget

| | Web Widget | Music Widget |
|--|------------|--------------|
| Purpose | Any http(s) page | Search / select / control music |
| Primary UI | WebView2 page | Native Secret Base chrome |
| External sites | The content | Optional browser open only |

## What works now

1. Native search UI (Enter / Search button)
2. Results list → select track → Current Track
3. Play / Pause / Next / Previous (capability-gated)
4. **Demo catalog** provider (honest in-memory catalog — used when Spotify is not connected)
5. Optional “Open web source…” → system browser (https via `WebUrlValidator`)
6. Layout persistence of sources + current track metadata (no tokens / artwork URLs)
7. **Spotify connect** from the Music widget: paste the Client ID (Client Secret optional). PKCE uses one fixed redirect URI, `http://127.0.0.1:43821/callback`, which must be registered in the Spotify dashboard. The client file stays in `%LocalAppData%\SecretBase\credentials\` and is not committed. Playback controls an active Spotify device (Premium). Secret Base does not stream audio.

## What is deferred

- YouTube OAuth / Data API
- Real audio output / Windows Audio session control
- Local library scan / player
- Secret Base AI (natural language) — **Command boundary exists**

## Command boundary (future AI)

See [music-commands.md](music-commands.md).

```
Natural language (future)
        ↓
   Secret Base AI (future)
        ↓
    MusicCommand
        ↓
 MusicCommandService  ← validates; no OS / FS / process
        ↓
   IMusicProvider
        ↓
 Demo / Spotify API / YouTube API / Local (as available)
```

AI must never call Providers or OS APIs directly.

## Providers & capabilities

| Provider | Capabilities |
|----------|----------------|
| `SpotifyMusicProvider` | Search, Playback, Pause, Resume, Next, Previous, NowPlaying, Authentication (active Spotify device) |
| `DemoCatalogMusicProvider` | Search, Playback, Pause, Resume, Next, Previous, NowPlaying |
| `OpenWebMusicProvider` | OpenInWidget only (URL → browser) |
| `LocalMusicProvider` | None (placeholder) |

UI disables unsupported transport buttons via `MusicProviderCapabilities`.

## Security

- No Host Bridge / WebMessage / host objects
- Music Widget primary path does **not** embed service UIs in WebView
- Commands cannot open files, spawn processes, or touch the registry
- Search rejects empty / path-like / `://` queries
- Persistence: metadata only — never tokens, cookies, passwords

Overlay / DWM / SetWindowRgn — unchanged.

## Windows manual checklist

1. Overlay click-through / Exit / restore still OK (**unverified on this Linux agent**).
2. Clock / Text / Web / Calendar unchanged.
3. ♪ → search “Lilac” → select → play/pause/next.
4. Open web source opens browser only (optional).
5. Theme Aa tints Music chrome.
6. Restart restores last Current Track title/artist.
