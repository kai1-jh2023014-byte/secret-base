# Windows logon auto-start

Secret Base can register itself in the **current user's** Windows startup list so the desktop overlay is available after logon.

## Mechanism

| Piece | Location |
|-------|----------|
| Contract | `IAutoStartService` (`Platform.Abstractions`) |
| Implementation | `WindowsRegistryAutoStartService` (`Platform.Windows`) |
| Preference | `AppLaunchSettings` → `%LocalAppData%\SecretBase\settings\launch.json` |
| Coordinator | `AutoStartCoordinator` (`Infrastructure`) |
| UI | **Setup** gear FAB (always visible) + Debug chrome toggle (Ctrl+Shift+D) |

Registration uses the standard per-user Run key:

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

Value name: `SecretBase` (see `AppInfo.ProductId`)

Command:

```text
"C:\Windows\System32\wscript.exe" //B //Nologo "C:\Users\me\AppData\Local\SecretBase\launch-secretbase.vbs" --autostart
```

The script sets `DOTNET_ROOT` when a user-local .NET install exists, then starts `SecretBase.App.exe`. It is saved as Shift-JIS (code page 932), the encoding Japanese Notepad and wscript already use. A shortcut that points straight at the exe flashes and exits, because Explorer does not inherit `DOTNET_ROOT`.

This appears in **Windows Settings → Apps → Startup** like other desktop applications. No elevation, no machine-wide shell changes, no PowerShell persistence.

## Development vs release

| Path | Auto-start |
|------|------------|
| `.\run.ps1` (dotnet run) | Supported when `SecretBase.App.exe` exists next to the build output — registration points at that **exe**, not `dotnet.exe` |
| `.\run.ps1 -ExeOnly` | Recommended — launches the apphost directly |
| Start Menu / Desktop shortcut | Supported (`.\install-launchers.ps1` or Setup → Create shortcuts) |
| Built apphost | `src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\SecretBase.App.exe` |

## Daily launch (no terminal)

```powershell
.\install-launchers.ps1
```

This always rebuilds Debug | x64 first. An exe left in `bin\` from an older checkout is not reused. The script prints the branch, commit, and the exe's timestamp.

Creates:

- `%AppData%\Microsoft\Windows\Start Menu\Programs\Secret Base.lnk`
- Desktop `\Secret Base.lnk`

Then open **Setup (⚙)** in the overlay and turn on **Start at login**.

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
