# Windows Manual QA — Resident App

Use after `.\build.ps1` on a Windows x64 machine.

## Build and launch

1. `.\build.ps1`
2. Run `src\SecretBase.App\bin\x64\Debug\net10.0-windows10.0.26100.0\SecretBase.App.exe` directly (not only `run.ps1` for auto-start tests)

## Auto-start

3. Ctrl+Shift+D → enable **Start at login**
4. Windows Settings → Apps → Startup — confirm **Secret Base** entry points to `SecretBase.App.exe --autostart`
5. Sign out / sign in — Secret Base starts once (overlay visible)
6. Launch `SecretBase.App.exe` again — second process exits immediately (single instance)
7. Ctrl+Shift+D → disable **Start at login**
8. Sign out / sign in — Secret Base does **not** start

## Development path

9. `.\run.ps1` still works for daily development
10. Auto-start toggle while using `run.ps1` shows an error (dotnet host cannot be registered)

## Phase 1 safety

11. Corrupt `%LocalAppData%\SecretBase\layouts\default.layout.json` → restart → default layout + `.corrupt-*` backup
12. Repeat for `themes\default.theme.json`

## Regression smoke

13. Overlay click-through, widget drag/resize/delete
14. Clock, Text, Web, Calendar, Music widgets open
15. Safe Exit (Ctrl+Shift+Q) ends process only
16. Assistant / AI — no automatic network calls on login (only after user interaction)
