# Windows logon auto-start

Secret Base can register itself in the **current user's** Windows startup list so the desktop overlay is available after logon.

## Mechanism

| Piece | Location |
|-------|----------|
| Contract | `IAutoStartService` (`Platform.Abstractions`) |
| Implementation | `WindowsRegistryAutoStartService` (`Platform.Windows`) |
| Preference | `AppLaunchSettings` → `%LocalAppData%\SecretBase\settings\launch.json` |
| Coordinator | `AutoStartCoordinator` (`App`) |
| UI | Debug chrome toggle (Ctrl+Shift+D) |

Registration uses the standard per-user Run key:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

Value name: `SecretBase` (see `AppInfo.ProductId`)

Command:

```text
"C:\path\SecretBase.App.exe" --autostart
```

This appears in **Windows Settings → Apps → Startup** like other desktop applications. No elevation, no machine-wide shell changes, no PowerShell persistence.

## Development vs release

| Path | Auto-start |
|------|------------|
| `.\run.ps1` / `dotnet run` | **Not supported** — host is `dotnet.exe`; toggle shows an error and does not register |
| Built `SecretBase.App.exe` (x64 Debug/Release) | Supported |

`run.ps1` remains the developer launch path and is never written to the startup registry.

## Single instance

Logon startup launches `SecretBase.App.exe` like a normal double-click. `ISingleInstanceGuard` runs first in `App.OnLaunched`; a second logon attempt exits without creating another overlay.

## Failure containment

When launched with `--autostart`, startup failures are **logged only** (no blocking dialog) so Windows logon is not interrupted. Manual launches still show `StartupFailurePresenter`.

## Preference sync

- Toggle **On** in Secret Base → registry entry + `launch.json`
- Toggle **Off** → remove registry entry + `launch.json`
- Disabled in Windows Startup apps → preference cleared on next Secret Base launch (not silently re-enabled)
- Enabled in Windows Startup apps → preference set to on on next launch
- Executable moved → stale path repaired on next launch when preference is on

## Uninstall / stale entries

Disabling auto-start in Secret Base or Windows removes the `SecretBase` Run value. Deleting the executable without disabling may leave a broken Startup entry (standard Windows behavior); reinstalling or re-enabling overwrites the path.

macOS uses a LaunchAgent instead of HKCU Run — see [macos.md](macos.md).
