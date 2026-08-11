# Windows Update Resilience

## Strategy

1. **Overlay, don't replace.** Secret Base is a separate process, not a shell substitute.
2. **Adapter isolation.** All OS-facing code lives in `SecretBase.Platform.Windows`.
3. **Public APIs first.** Prefer Windows App SDK / WinUI / documented .NET APIs.
4. **Compatibility snapshot.** `ICompatibilityService` records OS / .NET / WASDK / app versions at startup for diagnostics.
5. **Safe exit.** `ISafeExitService` only ends the Secret Base process.

## Explicitly forbidden (project policy)

- Explorer.exe injection / subclassing
- Undocumented Taskbar COM hooks for shell replacement
- Patching system files
- Replacing the Windows shell

## Recovery story

If Secret Base fails after a Windows Update:

1. User closes the app (or kills `SecretBase.App.exe`) → normal Desktop remains.
2. Delete `%LocalAppData%\SecretBase` to reset local config (layouts/themes/settings/logs).
3. Reinstall/rebuild the app without touching Windows shell state.

## Future Compatibility Check

`CompatibilityInfo` is the extension point for blocking known-bad OS/WASDK combinations once we have field data.
