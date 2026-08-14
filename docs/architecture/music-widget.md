# Music Widget & Music Hub (MVP)

Music is a first-class Creative OS surface on the Secret Base Desktop — not a generic Web window.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"music"` |
| Configuration | `MusicWidgetConfiguration` (`Sources`, `ActiveSourceId`) |
| View | `SecretBase.Widgets.Music.MusicWidgetView` |
| Core hub | `IMusicProvider` / `MusicService` |

## How to add

- Always-visible **♪** FAB
- Debug chrome **Add Music**
- **Ctrl+Shift+M**

Default layout still seeds only Clock + Text. Music is never auto-inserted.

## Web Widget vs Music Widget

| | Web Widget | Music Widget |
|--|------------|--------------|
| Purpose | Any http(s) page | Music **sources** as Desktop citizens |
| Model | Single `Url` | `MusicSource` list (Spotify / YouTube / Web / Local) |
| UX | URL toolbar browser | Hub → pick source → open |
| Security | Untrusted WebView2, no Host Bridge | **Same** when browsing |

## What works now

1. Add Music Widget (move / resize / theme / layout persistence)
2. Default sources: Spotify (`open.spotify.com`), YouTube Music, Local placeholder
3. **+ Add source** (Spotify / YouTube / Web / Local metadata)
4. Open web sources in hardened WebView2 (`WebUrlValidator` http/https only)
5. Local source shows a clear “not available yet” message (no FS scan)

## What is explicitly deferred

- Spotify OAuth / Spotify Web API
- YouTube OAuth / YouTube Data API
- Search, playlists, now-playing transport
- Local library / player / Windows audio integration
- DRM bypass, cookie extraction, Host Bridge

## Layers

| Layer | Owns |
|-------|------|
| **Core** | `MusicSource`, `MusicSourceType`, `MusicWidgetConfiguration`, `IMusicProvider`, `OpenWebMusicProvider`, `LocalMusicProvider`, `MusicService` |
| **Widgets** | `MusicWidgetView` (hub UI + Untrusted WebView2) |
| **App** | FAB / dialog / `WidgetFrame` host |
| **Infrastructure** | Existing `JsonLayoutStore` (opaque configuration bag) |

Core has **no** WebView2 / WinUI / Win32 dependency.

## Provider boundary (future)

```
Music Widget
     ↓
MusicService
     ↓
IMusicProvider (+ Capabilities / AuthStatus)
   ├─ OpenWebMusicProvider   (now — URL open only)
   ├─ LocalMusicProvider     (now — placeholder)
   ├─ SpotifyApiProvider     (future — official API + consent)
   └─ YouTubeApiProvider     (future — official API + consent)
```

Capabilities today: `OpenInWidget` only. Auth / Search / NowPlaying / LocalLibrary are reserved flags.

## Security

```
Music web page (Untrusted)
        ↓
     WebView2
        ↓
  NO HOST BRIDGE
  (AreHostObjectsAllowed = false,
   IsWebMessageEnabled = false)
        ↓
Secret Base Core / Platform  ← inaccessible
```

Persistence stores **source metadata only** (id, type, name, url, enabled). Never tokens, cookies, API keys, or passwords.

Overlay / DWM / SetWindowRgn — **unchanged**.

## Persistence fragment

```json
{
  "type": "music",
  "configuration": {
    "ActiveSourceId": "spotify-default",
    "Sources": [
      {
        "Id": "spotify-default",
        "Type": "Spotify",
        "Name": "Spotify",
        "Url": "https://open.spotify.com/",
        "IsEnabled": true
      }
    ]
  }
}
```

No `schemaVersion` bump — additive widget type.

## Windows manual checklist

1. Overlay click-through / edges / Exit / layout restore still OK.
2. Clock / Text / Web / Calendar unchanged.
3. **♪** / Ctrl+Shift+M → Music Hub with Spotify / YouTube / Local.
4. Open Spotify or YouTube → page loads in-widget; Back returns to sources.
5. Add source with `file://` or `javascript:` → rejected.
6. Theme **Aa** tints Music chrome.
7. Restart restores sources + geometry.
