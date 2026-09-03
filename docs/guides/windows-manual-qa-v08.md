# Windows manual QA — Secret Base v0.8

Linux CI cannot run the WinUI overlay. Use this checklist on a Windows x64 machine with .NET 10 SDK.

## Startup (Scenario A)

1. Cold boot or sign-in with AutoStart enabled (optional).
2. Secret Base overlay appears without a long pause.
3. Base / Clock is visible. **No LLM/Ollama boot is required** before the desktop appears.
4. Logs show observation capability (foreground process name + idle) or a warning that observation failed — overlay must still work.

## Resume (Scenario B)

1. Open a registered Creative Project from Secret Base, or prepare a workspace and Continue after confirmation.
2. Exit Secret Base (Ctrl+Shift+Q).
3. Relaunch. Base should show last session + next task when confidence is high.
4. Continue still shows a confirmation dialog. Apps must not auto-launch.

## Time awareness (Scenario C)

1. Add a local calendar block whose title matches a registered project (e.g. "Secret Base Development") that is happening now.
2. With recent project activity, Base should quietly offer continuation — not a toast storm.

## Focus (Scenario D)

1. Start a 25-minute focus timer from Base AI or the workspace tools.
2. Calendar/work suggestions must stay silent until focus ends.

## Cross-app (Scenario E)

1. Calendar block + open todo + recent project activity + remembered last session should produce a Continue suggestion with understandable copy (last session / next task).

## Learning (Scenario F)

1. Dismiss the same continuation three times. Later suggestions should get quieter (Passive), not more aggressive.
2. Accepting suggestions must **never** skip the Continue confirmation or launch allowlisted apps automatically.

## AI failure (Scenario G)

1. With no OpenAI/Gemini key and Local unavailable, Clock, Calendar, Todo, Memory, Search, and workspace prepare still work.
2. Base AI shows a calm unavailable state.

## Observation (Scenario H)

1. Switch to a registered app (e.g. Cursor) whose window title includes a registered project name.
2. Activity / user state should mention the project. Browser titles without a project name must not be stored.
3. Idle for 3+ minutes then return — an idle/resume activity may appear. No keystrokes or clipboard text.

## Safety / privacy

- No `shell.run` / PowerShell / arbitrary exe.
- `files_delete` only unregisters / returns a Block item after explicit confirmation.
- Memory refuses API keys and paths.
- Existing Clock/Text/Web/Calendar widgets on disk layouts remain.

## Performance

- Clock still ticks at 1s without recomputing the full automation pipeline every tick.
- Quiet automation is debounced (about 60s for time/application; calendar/startup/resume may evaluate sooner).
