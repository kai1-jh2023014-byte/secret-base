# Secret Base

> Make your PC feel like *your* secret base — a Personal Creative Desktop Environment for Windows and macOS.

**あなたの PC を、自分だけの秘密基地に。** Windows / macOS 向けのパーソナル・デスクトップ環境です。

**v0.6 Base Experience** は日常利用できるオーバーレイ完成形です。Explorer / Taskbar / Finder / Dock は置き換えません。

## What you can do / できること

| Area | Experience | 日本語 |
|------|------------|--------|
| **Overlay** | Wallpaper shows through; click outside widgets → normal Desktop | 壁紙透過。ウィジェット外クリックはそのままデスクトップ |
| **Widgets** | Clock, Text, Web, Calendar, Music, Workspace, AI, Creative, **My Apps**, **Progress / Genesis**, Pomodoro | 時計・テキスト・Web・カレンダー・音楽・ワークスペース・AI・制作・**マイアプリ**・**Progress/Genesis**・ポモドーロ |
| **Projects** | Create a Project → Dashboard → Open Folder / Cursor / ChatGPT | プロジェクト → ダッシュボード → フォルダ / Cursor / ChatGPT |
| **My Apps** | Register exe / folder / https and launch (no Shell, no admin) | exe・フォルダ・https を登録して起動（Shell / 管理者権限なし） |
| **Progress** | Live Professional Readiness % + MusicLab status (honest 0% when unknown) | Progress の準備度 % と MusicLab 状態（不明なら正直に 0%） |
| **Classroom** | Official Google Classroom in the **existing Web Widget** | 既存 Web ウィジェットで Classroom 公式サイトを開く |
| **AI** | Base AI: context → plan → confirm → action. Remote falls back to Local | Base AI。リモート失敗時は Local にフォールバック |
| **Theme** | Shared colors, fonts, corner radius, transparency | 色・フォント・角丸・透明度 |
| **Safe Exit** | **Ctrl+Shift+Q** — process end only | プロセス終了のみ。OS シェルは触らない |

## Quick start / はじめに

```powershell
.\build.ps1
.\test.ps1
.\install-launchers.ps1   # Start Menu + Desktop shortcuts (no terminal next time)
.\run.ps1 -ExeOnly        # launch SecretBase.App.exe (needed for Start at login)
```

Or for a quick dev loop: `.\run.ps1`

In the overlay, open **Setup (⚙)** → create shortcuts / enable **Start at login**. AI Settings saves OpenAI and Gemini keys to **separate** Credential Manager slots — use **Test Connection** after saving.

**macOS** (workspace window — does not replace Finder or Dock):

```bash
./run-mac.sh
```

Details: [docs/architecture/macos.md](docs/architecture/macos.md) · [windows-autostart.md](docs/architecture/windows-autostart.md)

| Shortcut | Action |
|----------|--------|
| **Ctrl+Shift+N** | Add Widget (catalog) |
| **Ctrl+Shift+B** | Add Block |
| **Ctrl+Shift+W** | Add Web |
| **Ctrl+Shift+C** | Add Calendar |
| **Ctrl+Shift+M** | Add Music |
| **Ctrl+Shift+E** | Add Creative (Projects) |
| **Ctrl+Shift+A** | Add AI Workspace |
| **Ctrl+Shift+T** | Theme |
| **Ctrl+Shift+D** | Debug chrome |
| **Ctrl+Shift+Q** | Safe Exit |

Full list: [docs/guides/keyboard-shortcuts.md](docs/guides/keyboard-shortcuts.md)

Bottom-left FABs: **+** Add Widget · **Blk** Block · **Aa** Theme · **⚙** Setup  
（デスクトップ全体の「整頓 / Grid Arrange」FAB は削除済み。Block 内のアイコン整頓は残っています。）

## My Apps — how to connect / 接続の仕方

詳しい日本語ガイドと JSON 形式: **[docs/architecture/my-apps.md](docs/architecture/my-apps.md)**

1. **+** → Add Widget → **My Apps**
2. **Register** で登録:
   - **Application** … `C:\Apps\tool.exe` など絶対パスの実行ファイル
   - **Folder** … `C:\src\project` など絶対パスのフォルダ
   - **Website** … `https://…` のみ（危険スキーム不可）
3. 任意で **Project root** を指定 → **Cursor** ボタンでそのフォルダを Cursor で開く
4. 保存先: `%LocalAppData%\SecretBase\apps\apps.json`（atomic 書き込み）

```json
{
  "schemaVersion": 1,
  "apps": [
    {
      "id": "a1b2c3d4e5f60718293a4b5c6d7e8f90",
      "name": "Pokemon Calculator",
      "description": "Damage calc",
      "launchTarget": "C:\\Apps\\PokemonCalc.exe",
      "type": 0,
      "projectRoot": "C:\\src\\pokemon-calc",
      "creativeProjectId": null,
      "dateAdded": "2026-10-08T00:00:00+00:00"
    }
  ]
}
```

`type`: `0` = Application · `1` = Folder · `2` = Website

## Progress / Genesis

- Default source: Progress learning API (`http://127.0.0.1:8001`) + MusicLab `genesis-status.json`
- Refresh: **on startup** (restore last log, then live probe) and **every 10 minutes**
- Minimal (transparent) mode keeps **% numbers + gauges**; × / mode icon hide until selected
- Docs: [docs/architecture/progress-widget.md](docs/architecture/progress-widget.md)

## Principles

1. **Do not break the OS shell** — overlay on Windows; workspace window on macOS. No Explorer/Taskbar/Dock/Finder surgery.
2. **Security first** — least privilege; dangerous OS actions stay out of scope.
3. **AI is never unrestricted** — Command → Service → validated Platform launch.
4. **Plugins are untrusted** — Core is not a plugin playground.
5. **Core ⊥ Platform** — OS APIs stay in `SecretBase.Platform.Windows` or `SecretBase.Platform.Mac`.

## Architecture (short)

```
App (WinUI overlay)  or  App.Mac (Avalonia workspace)
  → Widgets (WinUI) / Mac window chrome
  → Core (models, Commands, validation)
  → Infrastructure (JSON AppData)
  → Platform.Windows | Platform.Mac
```

Details: [docs/architecture/overview.md](docs/architecture/overview.md)

## Local data

```
Windows: %LocalAppData%\SecretBase\
macOS:   ~/Library/Application Support/SecretBase\
  apps\         # apps.json (My Apps registry)
  creative\     # workspace.json, projects.json
  layouts\
  themes\
  logs\
  settings\     # assistant.json, base.json, todos.json,
                # progress-genesis.json, progress-genesis-log.json, genesis-status.json
  block-items\  # per-Block intake folders
```

## Base AI in one paragraph

Base AI is Secret Base's quiet intelligence — not a ChatGPT clone. It observes local context (calendar, projects, todos, registered files/apps, music, focus), can **prepare a workspace** without launching anything, and asks for confirmation before opening a project or app. File cleanup is suggestion-only. `files_delete` never calls OS `File.Delete`. Missing API keys fall back to Local AI.

## Provider setup

Remote (OpenAI / Gemini) is optional. With no API key, Base AI uses **Local** (Ollama) when available, otherwise shows a calm unavailable state. Keys stay in Credential Manager / Keychain.

## Tech stack

| Piece | Choice |
|-------|--------|
| Language | C# |
| Runtime | **.NET 10 LTS** |
| UI | **WinUI 3** (Windows) / **Avalonia 11.3.14** (macOS) |
| Platform | **Windows App SDK 2.3.1** / **Platform.Mac** (LaunchAgent, Keychain, `open`) |
| Web | **WebView2** on Windows (Untrusted; no Host Bridge). macOS v1 has no WebView. |

## Docs

- [macOS host](docs/architecture/macos.md)
- [Overview](docs/architecture/overview.md)
- [My Apps（接続・形式）](docs/architecture/my-apps.md)
- [Progress / Genesis](docs/architecture/progress-widget.md)
- [Widget architecture](docs/architecture/widget-architecture.md)
- [Desktop overlay](docs/architecture/desktop-overlay.md)
- [Security boundaries](docs/architecture/security-boundaries.md)
- [Creative Workspace / Projects](docs/architecture/creative-workspace.md)
- [AI Workspace](docs/architecture/ai-workspace.md)
- [Secret Base AI](docs/architecture/ai-assistant.md)
- [Assistant Context](docs/architecture/assistant-context.md)
- [Assistant Tools](docs/architecture/assistant-tools.md)
- [AI Planning](docs/architecture/ai-planning.md)
- [AI Security](docs/architecture/ai-security.md)
- [Integration Hub (v0.2)](docs/architecture/integration-hub.md)
- [v0.2 Roadmap](docs/guides/v0.2-roadmap.md)
- [Decision log](docs/decisions/README.md)

## Intentionally out of scope

Unrestricted AI agents, MCP with OS power, Computer Use, arbitrary shell/file tools, Spotify/YouTube APIs, TimeTree/Notion Calendar APIs, Google Classroom API (open the official site only), Taskbar/Explorer/Shell hacks, auto-install, or admin elevation.
