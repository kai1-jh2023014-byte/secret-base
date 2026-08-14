# Decision Log

## 2026-08-11 — Adopt .NET 10 LTS + WASDK 2.3.1

**Decision:** Use .NET 10 (LTS) and Windows App SDK 2.3.1 with WinUI 3.

**Why:** WASDK compatibility is the top priority. .NET 10 is the only .NET line with Active LTS runway past late 2026. Official WinUI CLI templates target `net10.0-windows10.0.26100.0` and restore WASDK 2.3.1 cleanly.

## 2026-08-11 — Unpackaged self-contained app for v0.1

**Decision:** `WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`.

**Why:** Closing the process must return the user to the normal Windows desktop without MSIX residue. Self-contained reduces runtime install ambiguity while developing with Rider/CLI.

## 2026-08-11 — Layered solution before widgets

**Decision:** Ship solution skeleton + empty Desktop host before Clock/Text/Launcher.

**Why:** Establish Core/Platform separation and safe-exit path first so later features cannot accidentally couple to Win32.

## 2026-08-11 — Rider run configuration sharing

See [2026-08-11-rider-run-configuration.md](2026-08-11-rider-run-configuration.md).

## 2026-08-11 — Clock as reference widget

**Decision:** Implement Clock first with Core models + Infrastructure JSON + Widgets WinUI view + App host chrome.

**Why:** Establishes `WidgetInstance`, theme tokens, layout persistence, and drag/resize host patterns before adding more widget types.

## 2026-08-12 — Text Widget reuses WidgetFrame (no registry yet)

**Decision:** Add Text as a second built-in type (`WidgetTypes.Text`) with Core configuration + `TextWidgetView`, wired in `DesktopPage` beside Clock. No widget registry / plugin framework.

**Why:** A second concrete widget clarifies shared vs type-specific boundaries; abstract only after more types prove the pattern. Until Add Widget UI exists, Desktop may seed a missing Text instance on load.

## 2026-08-12 — Desktop Overlay via public AppWindow APIs

**Decision:** Evolve the host into a chromeless work-area overlay using `OverlappedPresenter.SetBorderAndTitleBar`, `DisplayArea.WorkArea`, transparent `SystemBackdrop`, and documented `DwmExtendFrameIntoClientArea`. Keep configuration in `IDesktopOverlayService` / `Platform.Windows`.

**Why:** Move toward “PC as secret base” without replacing Explorer/Taskbar. Defer pixel click-through until a public-API-safe approach is proven; do not use Explorer WorkerW or undocumented shell hooks.

## 2026-08-12 — Widgets-only overlay UX

**Decision:** Hide brand/status/Exit chrome in normal overlay UX; keep Safe Exit via **Ctrl+Shift+Q** and optional debug chrome via **Ctrl+Shift+D**. Soften `WidgetFrame` chrome (hover-emphasized grip/resize).

**Why:** Users should see wallpaper + floating widgets, not an app window. Recovery/exit must remain without putting chrome on the desktop permanently.

## 2026-08-12 — Transparent host + below-apps Z-order

**Decision:** Use a custom `SystemBackdrop` backed by `Windows.UI.Composition.Compositor.CreateColorBrush(transparent)` (after ensuring `Windows.System.DispatcherQueue`), plus documented `DwmExtendFrameIntoClientArea`, `DwmEnableBlurBehindWindow` (empty region), and `SetWindowSubclass` `WM_ERASEBKGND` FillRect(black). Park the host with documented `SetWindowPos(HWND_BOTTOM)` on configure/activate. Do **not** ABI-cast `Microsoft.UI.Composition` brushes onto `ICompositionSupportsSystemBackdrop.SystemBackdrop`.

**Why:** Microsoft.UI→Windows.UI brush casts FailFast on WASDK 2.3; `SystemBackdrop = null` alone leaves an opaque black client. Overlay must sit under other apps without Explorer WorkerW.

## 2026-08-12 — Widget-shaped input + suppress DWM edge frame

**Decision:** Shape the overlay HWND with documented `SetWindowRgn` (union of widget/block client rects from App → `IDesktopOverlayService.UpdateInteractiveInputRegions`). Suppress residual Win11 chrome with documented `DwmSetWindowAttribute(DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE)` and `DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_DONOTROUND`. Do **not** use `HTTRANSPARENT` for Desktop click-through (same-thread only). Do **not** use WorkerW / Explorer / Taskbar COM.

**Why:** Users need wallpaper/Desktop/other apps clickable outside widgets; WinUI transparency alone still hit-tests the full work-area HWND. `SetWindowRgn` is public and cross-process. Thin white edges after chromeless presenter are DWM border/corner artifacts, not XAML margins.

## 2026-08-13 — Overlay hit-test hardening (DesktopChildSiteBridge)

**Decision:** Keep Option A (`SetWindowRgn`), but apply the region to the top-level HWND **and** WinUI `DesktopChildSiteBridge` (and related) child HWNDs; map client rects to window-relative coordinates; cache/reapply on activation/`HWND_BOTTOM`; strengthen DWM edge suppress (`CAPTION_COLOR`, `SYSTEMBACKDROP_TYPE`, `SWP_FRAMECHANGED`). Still no WorkerW / Taskbar COM / undocumented APIs.

**Why:** Windows reports showed full-window input steal + edge chrome despite the first SetWindowRgn pass — consistent with shaping only the outer HWND while WinUI’s island child continued to hit-test the full client (see microsoft-ui-xaml#10746 / castorix notes).

See [overlay-input-and-edges.md](../architecture/overlay-input-and-edges.md).

## 2026-08-12 — Block as first-class Desktop room (schema v2)

**Decision:** Add `Block` / `BlockItem` models on `DesktopLayout.Blocks` (schemaVersion **2**). Host UI in App (`BlockFrame`); launch via `ITargetLaunchService` → `ShellTargetLaunchService` (`UseShellExecute` on absolute user-chosen paths). Upgrade v1 layouts by adding an empty `blocks` array without modifying widgets. Do not model Block as a Widget type yet; do not introduce a shared Frame base or registry.

**Why:** “Small rooms on the Desktop” needs nested items and safe launch, which is a different shape from Clock/Text configuration bags. Persistence compatibility for existing Clock/Text layouts is mandatory.

## 2026-08-13 — Theme editor for Clock / Text / Blocks

**Decision:** Ship a small Theme dialog (presets + hex colors + font/radius/opacity) that mutates the room `ThemeDefinition` and persists via existing `JsonThemeStore`. Re-render Desktop after Apply. No per-widget theme system yet.

**Why:** Users need to restyle Clock (time/date), Text boxes, and Blocks without a settings framework. One shared theme matches the current paint path (`ApplyTheme` / `BlockFrame.ApplyTheme`).

See [theme-editor.md](../architecture/theme-editor.md).

## 2026-08-13 — Desktop widget & block arrange

**Decision:** Add always-visible **Grid** FAB (+ debug **Arrange**) that runs `DesktopWidgetLayout.ArrangeEvenly` then `DesktopBlockLayout.ArrangeEvenlyBelow` (blocks under the widget band), persists layout JSON, and re-renders. Compact equal-gap grid from top-left — not full-bleed stretch.

**Why:** Users asked to tidy floating widgets/blocks without manual drag for every item; Core-only math keeps it testable and UI-agnostic.

## 2026-08-13 — Web Widget (WebView2, Untrusted)

**Decision:** Add `WidgetTypes.Web` with Core `WebWidgetConfiguration` + `WebUrlValidator` (http/https only). Host UI in `WebWidgetView` (WebView2 via WASDK). No schema bump. Not seeded into default layout — **Web** FAB / Ctrl+Shift+W. Harden WebView2 (`AreHostObjectsAllowed = false`, `IsWebMessageEnabled = false`, no host objects). Move/resize remain on `WidgetFrame`.

**Why:** Desktop-native web surfaces (YouTube first) without Electron, without Core→Windows coupling, and without a JS bridge into Secret Base.

See [web-widget.md](../architecture/web-widget.md) and [security-boundaries.md](../architecture/security-boundaries.md).

## 2026-08-13 — Calendar Widget + Integration Layer (local-only)

**Decision:** Add `WidgetTypes.Calendar` with Core `CalendarMonthBuilder` + `ICalendarEventSource` (`Empty` / `Local`). Host UI in `CalendarWidgetView`. Persist optional local events in widget configuration. Not seeded into default layout — **Cal** FAB / Ctrl+Shift+C. **Do not** change Desktop Overlay hit-test / DWM (PR #8 verified). No Outlook/Google APIs in v0.1.

**Why:** Month calendar on the Desktop with a provider-agnostic event layer, without cloud coupling or overlay redesign.

See [calendar-widget.md](../architecture/calendar-widget.md).

## 2026-08-14 — Calendar Today agenda + ICalendarProvider (Google ICS)

**Decision:** Evolve Calendar into a **Today agenda** widget (not a month grid). Introduce `ICalendarProvider` + `CalendarService`, common timed `CalendarEvent`, Core `IcsCalendarParser`, and Infrastructure `GoogleCalendarIcsProvider` (user-pasted secret iCal HTTPS URL — no OAuth client in app). Sample Creative-day agenda when unconfigured. **Do not** modify Overlay / DWM / SetWindowRgn.

**Why:** “Think / Manage” Creative OS layer — keep today's schedule visible; multi-provider ready without redesigning verified Desktop Overlay.

See [calendar-widget.md](../architecture/calendar-widget.md).

## 2026-08-14 — Calendar Hub / multi-provider Integration Layer

**Decision:** Enrich `ICalendarProvider` with `Capabilities` + `AuthStatus` + `CalendarInfo`; keep CRUD optional per provider. Ship `MockCalendarProvider`, JSON agenda cache (events only), `ISecureSecretStore` / Windows Credential Manager, and Google Calendar API **read** provider (loopback OAuth + PKCE, `calendar.readonly`) when the user supplies AppData OAuth client JSON. Document that **Notion Calendar** has no third-party sync API and **TimeTree Connect API** was terminated — do not ship fake providers; Web Widget/browser only. **Do not** modify Overlay / DWM / SetWindowRgn.

**Why:** Personal Desktop Environment needs a real multi-service hub without forcing unofficial APIs or conflating Notion Calendar with Notion Data API.

See [calendar-integration.md](../architecture/calendar-integration.md).

## 2026-08-14 — Music Widget / Music Hub MVP (web-open sources)

**Decision:** Add `WidgetTypes.Music` with Core `MusicSource` / `MusicWidgetConfiguration` / minimal `IMusicProvider` (OpenWeb + Local placeholder). Host UI is a Sources hub that opens Spotify/YouTube/Web URLs in Untrusted WebView2 with the same harden rules as Web Widget. No Spotify/YouTube OAuth or APIs. **Do not** modify Overlay / DWM / SetWindowRgn. Not seeded into default layout — **♪** FAB / Ctrl+Shift+M.

**Why:** Introduce Music as a Creative OS domain (not a generic Web Widget) while keeping security boundaries and deferring heavy API work.

See [music-widget.md](../architecture/music-widget.md).

## 2026-08-14 — Music native UX + MusicCommand boundary

**Decision:** Evolve Music Widget from WebView-primary hub to native search / current-track / transport UI. Expand `IMusicProvider` with capability-gated Search/Playback ops. Introduce `MusicCommand` + `MusicCommandService` so future AI must go Command → Service → Provider. Ship `DemoCatalogMusicProvider` (honest demo catalog) because Spotify/YouTube APIs are not connected. Optional web source open uses system browser only. **Do not** add Host Bridge, AI, or OAuth.

**Why:** Secret Base needs its own Music UX and a safe automation boundary before connecting AI or official music APIs.

See [music-widget.md](../architecture/music-widget.md) and [music-commands.md](../architecture/music-commands.md).

## 2026-08-14 — Creative Workspace Widget MVP

**Decision:** Add `WidgetTypes.Creative` as a desk for user-registered files/folders/projects (favorites, recent, search-registered-only, open via existing `ITargetLaunchService`). Persist items in AppData `creative/workspace.json` (`schemaVersion` 1), not layout JSON. Introduce `CreativeCommand` → `CreativeCommandService` for future AI. Path pickers via `IPathPickService` / Windows Storage pickers. **No** Explorer clone, delete/move/rename, Host Bridge, or Overlay changes.

**Why:** Personal Desktop Environment needs a safe “return to my work” surface distinct from Blocks (app tiles) and from a full file manager.

See [creative-workspace.md](../architecture/creative-workspace.md) and [creative-commands.md](../architecture/creative-commands.md).
