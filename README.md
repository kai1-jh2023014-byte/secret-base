# Secret Base

> Make your PC feel like *your* secret base — a Personal Desktop Environment for Windows.

**v0.1 milestone status:** foundation + Clock + Text + Blocks + Theme + Arrange + Desktop Overlay + Web + Calendar + Music + Creative Workspace + **Project Workspace** (**widgets-only UX**; overlay click-through verified).  
Wallpaper shows through the host; widgets float on the desktop; Exit via **Ctrl+Shift+Q** (debug chrome: **Ctrl+Shift+D**).

## Principles

1. **Do not break Windows** — overlay app; no Explorer/Taskbar surgery.
2. **Security first** — least privilege; dangerous OS actions are out of scope for now.
3. **AI is never unrestricted** — privilege ladder is modeled, not implemented yet.
4. **Plugins are untrusted** — Core is not a plugin playground.
5. **Core ⊥ Platform** — Windows APIs stay in `SecretBase.Platform.Windows`.

## Tech stack

| Piece | Choice |
|-------|--------|
| Language | C# |
| Runtime | **.NET 10 LTS** |
| UI | **WinUI 3** |
| Platform | **Windows App SDK 2.3.1** |
| Web | **WebView2** (via WASDK; Untrusted; no Electron) |
| IDE | Rider 2026.1 / `dotnet` CLI |
| Persistence | Split JSON under `%LocalAppData%\SecretBase` |

Details: [docs/architecture/tech-stack.md](docs/architecture/tech-stack.md)

## Solution layout

```
SecretBase/
├── src/
│   ├── SecretBase.App/
│   ├── SecretBase.Core/
│   ├── SecretBase.Infrastructure/
│   ├── SecretBase.Platform.Abstractions/
│   ├── SecretBase.Platform.Windows/
│   └── SecretBase.Widgets/
├── tests/
├── docs/architecture/
└── docs/decisions/
```

## Prerequisites

- Windows 11 (developed on Build 26200)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- WinUI project templates (once):

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

Optional: JetBrains Rider 2026.1

## Development

### Run

```powershell
.\run.ps1
```

In **Rider 2026.1**, use the shared Run/Debug configuration named **Secret Base** (stored in `.run/`, not `.idea/`).

### Build

```powershell
.\build.ps1
```

### Test

```powershell
.\test.ps1
```

Equivalent `dotnet` commands (same flags the scripts use):

```powershell
dotnet run --project src/SecretBase.App/SecretBase.App.csproj -c Debug -p:Platform=x64
dotnet build SecretBase.sln -c Debug -p:Platform=x64
dotnet test SecretBase.sln -c Debug
```

Use **Ctrl+Shift+Q** to leave Secret Base (Safe Exit). **Ctrl+Shift+D** toggles developer chrome. Closing the process returns you to the normal Windows desktop.

## Local data

```
%LocalAppData%\SecretBase\
  logs\
  layouts\    # reserved
  themes\     # reserved
  settings\   # reserved
```

## Roadmap after Calendar Hub

1. App Launcher polish  
2. Drag & Drop polish  
3. Room switching  
4. Additional calendar/data providers only when official APIs exist (e.g. Notion *data*, never fake TimeTree sync)

## Architecture docs

- [Overview](docs/architecture/overview.md)
- [Tech stack](docs/architecture/tech-stack.md)
- [Widget architecture](docs/architecture/widget-architecture.md)
- [Web Widget](docs/architecture/web-widget.md)
- [Calendar Widget](docs/architecture/calendar-widget.md)
- [Calendar Integration Layer](docs/architecture/calendar-integration.md)
- [Music Widget](docs/architecture/music-widget.md)
- [Music Commands](docs/architecture/music-commands.md)
- [Creative Workspace](docs/architecture/creative-workspace.md)
- [Creative Commands](docs/architecture/creative-commands.md)
- [Desktop overlay](docs/architecture/desktop-overlay.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Windows Update resilience](docs/architecture/windows-update-resilience.md)
- [Decision log](docs/decisions/README.md)
