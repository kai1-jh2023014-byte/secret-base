# Assistant Tools

Registered tools the LLM may call. Unknown names (including `shell.run`) are rejected. **HostAction is never registered.**

OpenAI function names use underscores (no `.`).

## Capability

| Capability | Behavior |
|------------|----------|
| **ReadOnly** | Auto-run. Observation only. |
| **Suggest** | Auto-run. Recommendation text only — never launches Host. |
| **RequiresConfirmation** | Pause → Cancel / Run → Command → Host if needed. |
| **HostAction** | Platform-only. **Never** LLM-callable. |

```
ReadOnly / Suggest
  → execute immediately → return text to model

RequiresConfirmation
  → Confirmation UI (single or multi)
  → User Run
  → Command → Service → Host launch (if ShouldLaunch / ShouldOpenCursorAtFolder)
```

## Tool catalog (v0.6)

### ReadOnly

| Tool | Routes to |
|------|-----------|
| `assistant_get_context` | `IAssistantContextService.GetSnapshotAsync` (includes remembered integrations) |
| `calendar_get_today` | `CalendarCommand.GetTodayEvents` |
| `calendar_get_upcoming` | `CalendarCommand.GetUpcoming` |
| `creative_list_projects` | `CreativeCommand.SearchProjects` |
| `creative_get_project` | `CreativeCommand.GetCreativeProject` |
| `apps_list` | `AppCommand.ListApps` |
| `music_search` | `MusicCommand.SearchTrack` (if Search capability) |
| `music_get_state` | Context / MusicService state |

### Suggest

| Tool | Behavior |
|------|----------|
| `project_recommend` | Candidate project from favorites/recent |
| `schedule_recommend` | Calendar + projects candidates (not life assertions) |
| `music_recommend` | Catalog candidate; demo-catalog honesty |

### RequiresConfirmation

| Tool | Routes to |
|------|-----------|
| `calendar_add_event` | Local `CalendarCommand.AddEvent` (Calendar widget; not Google write) |
| `calendar_remember_usual` | Remember a local usual slot |
| `calendar_apply_usual` | Apply remembered usual schedule onto today |
| `creative_open_project` | `CreativeCommand.OpenCreativeProject` |
| `cursor_open_project` | `AiCommand.OpenProjectInCursor` → Host `ICursorLaunchService` |
| `integration_open` | `IntegrationCommandService` (`classroom` / `calendar`) |
| `apps_open` | `AppCommand.OpenApp` → Host |
| `music_play` | `track_id` or `query` → Music widget (Spotify if connected) |
| `workspace_open_named` | Open Block item / My App / Creative Project by **name** |
| `workspace_remove` / `files_delete` | Return Block item to Desktop or unregister. **Never OS `File.Delete`.** |

## Argument validation

- JSON object only
- Rejects path-like / scheme-like / command-line-looking strings
- Bounded lengths

## Music honesty

If only the demo catalog supports search/playback, tool results say so. Spotify/YouTube API playback is never invented. After Spotify is connected, play stays in the Music widget.

Connected integrations (Spotify, Google Calendar) are remembered in `settings/integrations.json` (flags only — tokens stay in the OS secret store). Classroom is remembered as “opened / web widget” because this repo has no Classroom API.

## Disk safety

`files_delete` and `workspace_remove` never call OS `File.Delete` on user files. They return a hidden Block item to Desktop or unregister a Secret Base item. Unregistered disk paths are refused.

## Multi-tool / multi-action

Several ReadOnly/Suggest tools may run per turn (capped by `MaxSteps`). Multiple RequiresConfirmation tools in one model response are batched into one Confirm panel.

v0.5 UI also distinguishes Plan step kinds (`[Read]`, `[Suggest]`, `[Action]`) so the user can see what is observation versus execution.

## Related

- [ai-assistant.md](ai-assistant.md)
- [assistant-context.md](assistant-context.md)
- [assistant-planning.md](assistant-planning.md)
- [assistant-security.md](assistant-security.md)
