# Secret Base

> Make your PC feel like *your* secret base — a Personal Desktop Environment for Windows.

**v0.1 milestone status:** foundation only.  
You can launch a Secret Base Desktop window and safely return to the normal Windows desktop. Widgets, persistence, themes, and Web content come next.

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
| Web (later) | WebView2 |
| IDE | Rider 2026.1 / `dotnet` CLI |
| Persistence (later) | Split JSON under `%LocalAppData%\SecretBase` |

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

Use **Exit to Windows Desktop** (or close the window) to leave Secret Base. This only ends the app process.

## Local data

```
%LocalAppData%\SecretBase\
  logs\
  layouts\    # reserved
  themes\     # reserved
  settings\   # reserved
```

## Roadmap after this foundation

1. Clock Widget  
2. Text Widget  
3. App Launcher Widget  
4. Drag & Drop  
5. Resize  
6. Persistence (JSON)  
7. Theme system  
8. Web Content Widget  

## Architecture docs

- [Overview](docs/architecture/overview.md)
- [Tech stack](docs/architecture/tech-stack.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Windows Update resilience](docs/architecture/windows-update-resilience.md)
- [Decision log](docs/decisions/README.md)
