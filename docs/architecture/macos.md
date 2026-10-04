# macOS host

Secret Base on macOS is a **second head** that reuses Core, Infrastructure, and Platform.Abstractions. It does **not** run WinUI 3 / Windows App SDK.

```
Portable: Core, Infrastructure, Platform.Abstractions
Windows:  Platform.Windows + App + Widgets (WinUI) — unchanged
macOS:    Platform.Mac + App.Mac (Avalonia workspace window)
```

## What v1 is

A **workspace window** (Avalonia) with:

- Clock (same `ClockDisplayFormatter` + layout JSON)
- Secret Base AI (same `AssistantService`, confirmation gate, provider fallback)
- Login auto-start via a per-user LaunchAgent
- Safe Exit = process end only (**Ctrl+Shift+Q**)

It is **not** a wallpaper overlay with click-through (no `SetWindowRgn` equivalent in v1). It does **not** replace Finder, Dock, or the menu bar.

## What v1 is not

- Dock / menu bar / Finder replacement or injection
- A 1:1 port of every WinUI widget (WebView2, overlay hit-testing, Blocks chrome, Pomodoro timer UI)
- Signed `.app` notarization (publish the apphost; packaging can follow)

`focus_start` still runs through Base AI on macOS (shared `FocusSessionStore`), but there is no Avalonia Pomodoro widget yet — the WinUI Pomodoro surface is Windows-only for now.

## Platform adapters (`SecretBase.Platform.Mac`)

| Contract | macOS implementation |
|----------|----------------------|
| `IAutoStartService` | `~/Library/LaunchAgents/com.secretbase.app.plist` (`RunAtLoad`, Aqua session). No `KeepAlive`. |
| `ISingleInstanceGuard` | Exclusive file lock under Application Support |
| `ISecureSecretStore` | Keychain (`SecKeychain*` generic password). Tests / non-macOS: 0600 file store. |
| `ITargetLaunchService` | `/usr/bin/open` + validated absolute path only |
| `ICursorLaunchService` | `/Applications/Cursor.app` (or `~/Applications`) via `open -a` |
| `IPathPickService` | Avalonia `StorageProvider` in `App.Mac` |
| `IFileIconService` | Stub (null) in v1 — launch still works |
| `IBlockItemIntakeService` | Move Desktop `.webloc`; never relocate `.app` bundles |
| `IDesktopOverlayService` | No-op workspace window (not Dock-behind wallpaper) |
| `ICompatibilityService` | OS / .NET snapshot; WASDK field = `n/a (macOS host)` |
| `ISafeExitService` | Process exit only |

## Local data

`Environment.SpecialFolder.LocalApplicationData` on macOS is:

```text
~/Library/Application Support/SecretBase/
```

Same JSON layout/theme/settings schema as Windows. API keys stay in Keychain, not JSON.

## Run / publish

```bash
chmod +x ./run-mac.sh
./run-mac.sh
```

`dotnet run` cannot register a LaunchAgent (host is `dotnet`). Publish the apphost, then enable **Login** in the window:

```bash
dotnet publish src/SecretBase.App.Mac/SecretBase.App.Mac.csproj -c Release -r osx-arm64 --self-contained
# or osx-x64
```

## Linux CI

`Platform.Mac` and `App.Mac` target `net10.0` so they restore/compile without Apple SDKs. Keychain P/Invoke is not exercised on Linux; LaunchAgent / path / secret-file tests run on Linux.

This agent cannot produce a signed Mac `.app`. Manual QA is on a Mac with .NET 10.
