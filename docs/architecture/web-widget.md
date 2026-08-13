# Web Widget

Web Widget embeds http(s) pages on the Secret Base Desktop via **WebView2**.
YouTube is the first example URL — not a special-cased integration.

## Type

| Field | Value |
|-------|-------|
| `WidgetTypes` | `"web"` |
| Configuration | `WebWidgetConfiguration` (`Url`) |
| View | `SecretBase.Widgets.Web.WebWidgetView` |

## How to add

- Always-visible **Web** FAB (bottom-left)
- Debug chrome **Add Web**
- **Ctrl+Shift+W**

Default layout still seeds only Clock + Text. Web is never auto-inserted.

## Layers

| Layer | Owns |
|-------|------|
| **Core** | `WebWidgetConfiguration`, `WebUrlValidator` (http/https only) |
| **Widgets** | `WebWidgetView` (WebView2 UI, loading/error, minimal toolbar) |
| **App** | `DesktopPage` wiring + Add dialog; `WidgetFrame` for move/resize |
| **Infrastructure** | Existing `JsonLayoutStore` (opaque `configuration` bag) |

Core has **no** WebView2 / WinUI / Win32 dependency.

## UI (v0.1)

Minimal toolbar inside the widget:

- URL box + **Go** + **Reload**
- Loading / error overlay
- Page content in WebView2

Move uses `WidgetFrame` **DragBar** (above content). Resize uses the frame corner grip.
Do not drag from the WebView surface.

## URL policy

Allowed: `http`, `https` (bare hosts like `www.youtube.com` → `https://…`).

Blocked: `file`, `javascript`, `data`, `vbscript`, `about`, and any other scheme.

Blocked navigations show: **This URL cannot be opened in Secret Base.**

## Security (mandatory)

```
Web page (Untrusted)
        ↓
     WebView2
        ↓
  NO HOST BRIDGE
  (AreHostObjectsAllowed = false,
   IsWebMessageEnabled = false,
   no AddHostObjectToScript)
        ↓
Secret Base Core / Platform  ← inaccessible from the page
```

See [security-boundaries.md](security-boundaries.md).

## Persistence

Same layout JSON as Clock/Text. Example fragment:

```json
{
  "type": "web",
  "position": { "x": 96, "y": 96 },
  "size": { "width": 560, "height": 360 },
  "configuration": {
    "Url": "https://www.youtube.com/"
  }
}
```

No `schemaVersion` bump — widget types are additive inside `widgets[]`.

## Windows manual checklist

Run on Windows 11 with WebView2 Runtime (usually present via Edge / WASDK):

```powershell
git checkout cursor/web-widget-6d90
git pull
.\run.ps1
```

1. Clock + Text still appear and work.
2. Tap **Web** → create with `https://www.youtube.com/` → page loads.
3. Drag via the top grip; resize via the corner — WebView must not steal move.
4. Change URL in the toolbar → Go → navigates; Exit and restart → URL/position restored.
5. Try `file:///C:/Windows/notepad.exe` or `javascript:alert(1)` → blocked message.
6. Theme **Aa** still tints the Web Widget chrome/toolbar.
7. Confirm no Secret Base APIs are exposed to the page (no host objects / web messaging).
