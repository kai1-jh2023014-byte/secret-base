# Operational Safety (Phase 1)

Hardening for daily-driver use: one overlay process, visible startup failures, and recoverable corrupt configuration.

## Single instance

| Piece | Location |
|-------|----------|
| Contract | `ISingleInstanceGuard` (`SecretBase.Platform.Abstractions`) |
| Implementation | `WindowsMutexSingleInstanceGuard` (`SecretBase.Platform.Windows`) |
| Integration | `App.OnLaunched` — before overlay creation |

Mutex name: `Local\SecretBase.App.SingleInstance` (per-user `Local\` namespace, no elevation).

- First launch: `TryAcquire()` succeeds → normal startup.
- Second launch: `TryAcquire()` fails → log, dispose bootstrap logger, `Exit()` (no overlay, no IPC).
- Release: `Dispose()` on main window `Closed` (process exit also releases the mutex).

Automated tests: `tests/SecretBase.Platform.Windows.Tests/SingleInstanceGuardTests.cs` (Windows only).

### Manual QA (Windows)

1. Start Secret Base.
2. Launch `SecretBase.App.exe` again.
3. Confirm only one overlay exists and layout JSON is unchanged.

## Startup failure containment

`App.OnLaunched` wraps startup in `try/catch`:

1. Log the exception via `FileAppLogger` (`startup` category).
2. Show `StartupFailurePresenter` (minimal `ContentDialog`: message, **Open Data Folder**, **Close**).
3. Exit the process (`Exit()` with `Environment.Exit(1)` fallback).

Does **not** delete AppData, change the registry, or start external tools. Recovery UI failures are swallowed so shutdown still completes.

## Corrupt layout / theme recovery

| Piece | Location |
|-------|----------|
| Backup helper | `CorruptJsonFileRecovery` (`SecretBase.Infrastructure`) |
| Layout store | `JsonLayoutStore.LoadOrCreateDefault` |
| Theme store | `JsonThemeStore.LoadOrCreateDefault` |

On missing, empty, null-deserialize, or `JsonException`:

1. Log a warning (and error for parse exceptions).
2. Move the bad file to `{stem}.corrupt-{utc-timestamp}{ext}` when possible (original not deleted in place).
3. Restore `DesktopLayout.CreateDefault()` or `ThemeDefinition.CreateDefault()` and save.

Backup failure is logged; startup continues with defaults. Atomic write (`.tmp` → copy → delete) is unchanged for normal saves.

Automated tests: `tests/SecretBase.Infrastructure.Tests/CorruptJsonRecoveryTests.cs`.

### Manual QA (Windows)

1. Exit Secret Base.
2. Corrupt `%LocalAppData%\SecretBase\layouts\default.layout.json` (or `themes\default.theme.json`).
3. Restart — app should start with defaults.
4. Confirm a `*.corrupt-*` backup exists beside the restored file.
