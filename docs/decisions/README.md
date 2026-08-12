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
