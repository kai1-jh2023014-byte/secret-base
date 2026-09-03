# Windows manual QA — v1.0 Personal AI OS

Linux CI cannot run the WinUI overlay. Walk this list on a Windows 11 x64 machine after `.\run.ps1`.

## Startup

1. Cold start with no network. Overlay appears. Clock ticks. No LLM required.
2. Second instance is refused (single instance).
3. After a previous session, Base card / briefing can offer Continue. **Nothing launches until you confirm.**

## Command palette (Ctrl+Space)

1. Overlay lists Continue, Today's schedule, Focus, Capture, Search memory.
2. Arrow keys + Enter. Esc closes.
3. Typing "focus" ranks the focus item.
4. Continue still shows confirmation. Cancel launches nothing.

## Daily briefing / Continue

1. With a calendar event and an open todo, briefing shows both as structured lines (not a chat paragraph).
2. 「昨日の続きをやりたい」 in Base AI explains why (evidence + confidence) and waits for Continue.
3. Confirm prepares workspace; registered apps still need a separate confirm to launch.

## Quick capture

1. Ctrl+Space → Quick capture → 「DTM AIにコード進行生成を追加したい」.
2. Destination is a suggestion. Save as Idea or Todo. Nothing else is rewritten.

## Focus

1. 「30分集中したい」 starts a local timer. No apps launch.
2. Attention / quiet suggestions stay silent while focus runs (unless you enabled interruptions).

## Memory / Privacy

1. Memory dialog lists source, importance, expiry. Forget removes the row.
2. Privacy dialog lists Observed / Remembered / Allowed / Confirmation / Never.
3. Quiet hours 22:00–08:00: low-urgency suggestions do not appear.

## Observation

1. Switching to a registered app (e.g. Cursor) records ApplicationOpened (name only).
2. Chrome title without a registered project name is not stored.
3. Idle after a few minutes records IdleStarted. No keystrokes, no clipboard.

## Automation

1. Automation permission dialog can disable the startup Continue rule.
2. Accepting Continue increases ranking; dismissing music-like suggestions quiets them.
3. After many accepts, Continue **still** requires confirmation.

## AI unavailable

1. Remove API keys. Calendar, Todo, Search, Palette, Briefing, Workspace still work.
2. Base AI answers briefing/search/focus without calling a model. Other chat shows the usual unavailable copy.

## Safety

1. There is no way to run PowerShell or an arbitrary exe from Base AI.
2. File cleanup shows candidates only. Confirming `files_delete` returns a Block item — the file stays on disk.
