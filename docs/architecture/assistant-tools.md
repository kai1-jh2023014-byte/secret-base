# Assistant Tools

Registered tools the LLM may call. Unknown names (including `shell.run`) are rejected.

OpenAI function names use underscores (no `.`).

## Capability

| Capability | Behavior |
|------------|----------|
| **ReadOnly** | Auto-run. Observation only. |
| **RequiresConfirmation** | Pause → Cancel / Run → then Command → Host if needed. |
| **HostAction** | Reserved for Platform. **Never** LLM-callable. |

```
ReadOnly tool
  → execute immediately → return text to model

RequiresConfirmation tool
  → Confirmation UI
  → User Run
  → Command → Service → Host launch (if ShouldLaunch / ShouldOpenCursorAtFolder)
```

## Tool catalog (v0.3)

### ReadOnly

| Tool | Routes to |
|------|-----------|
| `assistant_get_context` | `IAssistantContextService.GetSnapshotAsync` |
| `calendar_get_today` | `CalendarCommand.GetTodayEvents` |
| `calendar_get_upcoming` | `CalendarCommand.GetUpcoming` |
| `creative_list_projects` | `CreativeCommand.SearchProjects` |
| `creative_get_project` | `CreativeCommand.GetCreativeProject` |
| `apps_list` | `AppCommand.ListApps` |
| `music_search` | `MusicCommand.SearchTrack` (if Search capability) |
| `music_get_state` | Context / MusicService state |

### RequiresConfirmation

| Tool | Routes to |
|------|-----------|
| `creative_open_project` | `CreativeCommand.OpenCreativeProject` |
| `cursor_open_project` | `AiCommand.OpenProjectInCursor` → Host `ICursorLaunchService` |
| `integration_open` | `IntegrationCommandService` (`classroom` / `calendar`) |
| `apps_open` | `AppCommand.OpenApp` → Host `ITargetLaunchService` / browser |
| `music_play` | `MusicCommand.PlayTrackById` (if Playback capability) |

## Argument validation

- JSON object only
- Rejects path-like / scheme-like / command-line-looking strings
- Bounded lengths

## Music honesty

If only the demo catalog supports search/playback, tool results say so. Spotify/YouTube API playback is never invented.

## Multi-tool reads

The chat loop allows several **ReadOnly** tools per turn (and across rounds, capped) so prioritization answers can combine Calendar + Projects. Launch tools still stop for confirmation.

## Related

- [ai-assistant.md](ai-assistant.md)
- [assistant-context.md](assistant-context.md)
