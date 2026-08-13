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

