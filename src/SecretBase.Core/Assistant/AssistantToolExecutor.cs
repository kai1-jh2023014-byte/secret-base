using System.Text.Json;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Routes registered tools to existing Command services / read-only context.
/// Never launches OS processes. HostAction tools are rejected.
/// </summary>
public sealed class AssistantToolExecutor : IAiToolExecutor
{
    private readonly IAiToolRegistry _registry;
    private readonly CalendarCommandService? _calendar;
    private readonly CreativeCommandService? _creative;
    private readonly AiCommandService? _ai;
    private readonly IntegrationCommandService? _integration;
    private readonly AppCommandService? _apps;
    private readonly MusicCommandService? _music;
    private readonly IAssistantContextService? _context;

    public AssistantToolExecutor(
        IAiToolRegistry registry,
        CalendarCommandService? calendar = null,
        CreativeCommandService? creative = null,
        AiCommandService? ai = null,
        IntegrationCommandService? integration = null,
        AppCommandService? apps = null,
        MusicCommandService? music = null,
        IAssistantContextService? context = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _calendar = calendar;
        _creative = creative;
        _ai = ai;
        _integration = integration;
        _apps = apps;
        _music = music;
        _context = context;
    }

    public async Task<AssistantToolResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        var definition = _registry.Find(toolName);
        if (definition is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (AssistantConfirmationPolicy.IsHostActionOnly(definition))
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: "Host");
        }

        if (!AssistantToolArgumentValidator.TryParseObject(argumentsJson, out var root, out var parseError))
        {
            return AssistantToolResult.Fail(parseError);
        }

        return toolName switch
        {
            AssistantToolNames.AssistantGetContext => await GetContextAsync(cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CalendarGetToday => await CalendarTodayAsync(cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CalendarGetUpcoming => await CalendarUpcomingAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CreativeListProjects => CreativeList(),
            AssistantToolNames.CreativeGetProject => await CreativeGetAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CreativeOpenProject => CreativeOpen(root),
            AssistantToolNames.CursorOpenProject => CursorOpen(root),
            AssistantToolNames.IntegrationOpen => await IntegrationOpenAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.AppsList => AppsList(),
            AssistantToolNames.AppsOpen => AppsOpen(root),
            AssistantToolNames.MusicSearch => await MusicSearchAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.MusicGetState => MusicGetState(),
            AssistantToolNames.MusicPlay => await MusicPlayAsync(root, cancellationToken).ConfigureAwait(false),
            _ => AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable)
        };
    }

    private async Task<AssistantToolResult> GetContextAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activity: "Context unavailable",
                activityDomain: AssistantActivityDomains.Context);
        }

        var snapshot = await _context.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var text = AssistantContextService.FormatForModel(snapshot);
        if (text.Contains("sk-", StringComparison.OrdinalIgnoreCase)
            || text.Contains("apiKey", StringComparison.OrdinalIgnoreCase)
            || text.Contains("Bearer ", StringComparison.Ordinal))
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activity: "Context blocked (secrets)",
                activityDomain: AssistantActivityDomains.Context);
        }

        return AssistantToolResult.Ok(
            text,
            activity: "Context ✓",
            activityDomain: AssistantActivityDomains.Context);
    }

    private async Task<AssistantToolResult> CalendarTodayAsync(CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activity: "Calendar unavailable",
                activityDomain: AssistantActivityDomains.Calendar);
        }

        var result = await _calendar.ExecuteAsync(CalendarCommand.GetTodayEvents(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, "today"),
            activity: "Calendar ✓",
            activityDomain: AssistantActivityDomains.Calendar);
    }

    private async Task<AssistantToolResult> CalendarUpcomingAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "days", 7, 1, 14, out var days, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        var result = await _calendar.ExecuteAsync(CalendarCommand.GetUpcoming(days), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, $"next {days} day(s)"),
            activity: "Calendar ✓",
            activityDomain: AssistantActivityDomains.Calendar);
    }

    private AssistantToolResult CreativeList()
    {
        if (_creative is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Projects);
        }

        var result = _creative.Execute(CreativeCommand.SearchProjects(null));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Projects);
        }

        var lines = result.Projects.Select(p => $"{p.Id}: {p.Name}").ToList();
        var body = lines.Count == 0 ? "No Creative Projects are registered." : string.Join('\n', lines);
        return AssistantToolResult.Ok(
            body,
            activity: "Projects ✓",
            activityDomain: AssistantActivityDomains.Projects);
    }

    private async Task<AssistantToolResult> CreativeGetAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (!AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Projects);
        }

        if (_context is not null)
        {
            var summary = await _context.GetProjectAsync(id, cancellationToken).ConfigureAwait(false);
            if (summary is null)
            {
                return AssistantToolResult.Fail(
                    "Project is not registered.",
                    activity: "Projects ✗",
                    activityDomain: AssistantActivityDomains.Projects);
            }

            return AssistantToolResult.Ok(
                AssistantContextService.FormatProjectForModel(summary),
                activity: "Projects ✓",
                activityDomain: AssistantActivityDomains.Projects);
        }

        if (_creative is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Projects);
        }

        var result = _creative.Execute(CreativeCommand.GetCreativeProject(id));
        if (!result.Succeeded || result.Project is null)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? "Project is not registered.",
                activity: "Projects ✗",
                activityDomain: AssistantActivityDomains.Projects);
        }

        var project = result.Project;
        var quick = project.Resources.Where(r => r.IsQuickAction).Select(r => r.Name).Take(8).ToArray();
        var summaryFallback = new AssistantProjectSummary
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            NotesPreview = project.Notes,
            ProjectType = project.ProjectType.ToString(),
            IsFavorite = project.IsFavorite,
            LastOpened = project.LastOpened,
            QuickActionNames = quick,
            HasRootFolder = !string.IsNullOrWhiteSpace(project.RootFolder)
        };
        return AssistantToolResult.Ok(
            AssistantContextService.FormatProjectForModel(summaryFallback),
            activity: "Projects ✓",
            activityDomain: AssistantActivityDomains.Projects);
    }

    private AssistantToolResult CreativeOpen(JsonElement root)
    {
        if (_creative is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Projects);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Projects);
        }

        var result = _creative.Execute(CreativeCommand.OpenCreativeProject(id));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Projects);
        }

        var name = result.Project?.Name ?? id;
        return AssistantToolResult.Ok(
            $"Opened Creative Project dashboard for {name}.",
            activity: $"Projects ✓ · opened {name}",
            activityDomain: AssistantActivityDomains.Projects);
    }

    private AssistantToolResult CursorOpen(JsonElement root)
    {
        if (_ai is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Cursor);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Cursor);
        }

        var result = _ai.Execute(AiCommand.OpenProjectInCursor(id));
        if (!result.Succeeded)
        {
            var message = result.OfferCursorWebsiteFallback
                ? AssistantUserMessages.CursorOpenFailed
                : (result.ErrorMessage ?? AssistantUserMessages.CursorOpenFailed);
            return AssistantToolResult.Fail(
                message,
                activity: "Cursor ✗",
                activityDomain: AssistantActivityDomains.Cursor);
        }

        return AssistantToolResult.Ok(
            "Host may open this project in Cursor.",
            activity: "Cursor ✓",
            activityDomain: AssistantActivityDomains.Cursor,
            shouldOpenCursorAtFolder: result.ShouldOpenCursorAtFolder,
            cursorFolderPath: result.FolderPath);
    }

    private async Task<AssistantToolResult> IntegrationOpenAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_integration is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Integration);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "target", required: true, out var target, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Integration);
        }

        var commandId = target.ToLowerInvariant() switch
        {
            "classroom" => IntegrationCommandIds.ClassroomOpen,
            "calendar" => IntegrationCommandIds.CalendarOpen,
            _ => null
        };
        if (commandId is null)
        {
            return AssistantToolResult.Fail(
                "Unknown integration target. Use classroom or calendar.",
                activityDomain: AssistantActivityDomains.Integration);
        }

        var result = await _integration.ExecuteAsync(
            new IntegrationRequest { CommandId = commandId },
            cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Integration);
        }

        return AssistantToolResult.Ok(
            $"Open {target} at {result.LaunchTarget}.",
            activity: "Integration ✓",
            activityDomain: AssistantActivityDomains.Integration,
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private AssistantToolResult AppsList()
    {
        if (_apps is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Apps);
        }

        var result = _apps.Execute(AppCommand.ListApps());
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Apps);
        }

        var lines = result.Apps.Select(a => $"{a.Id}: {a.Name}").ToList();
        var body = lines.Count == 0 ? "No apps are registered." : string.Join('\n', lines);
        return AssistantToolResult.Ok(
            body,
            activity: "Apps ✓",
            activityDomain: AssistantActivityDomains.Apps);
    }

    private AssistantToolResult AppsOpen(JsonElement root)
    {
        if (_apps is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Apps);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "app_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Apps);
        }

        var result = _apps.Execute(AppCommand.OpenApp(id));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Apps);
        }

        return AssistantToolResult.Ok(
            $"Host may launch {result.App?.Name ?? id}.",
            activity: "Apps ✓",
            activityDomain: AssistantActivityDomains.Apps,
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private async Task<AssistantToolResult> MusicSearchAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Music);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "query", required: true, out var query, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Music);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Search))
        {
            return AssistantToolResult.Fail(
                "Music search is not available. No search-capable provider is configured.",
                activityDomain: AssistantActivityDomains.Music);
        }

        var result = await _music.ExecuteAsync(MusicCommand.SearchTrack(query), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Music);
        }

        var providers = _music.MusicService.Providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .Select(p => p.DisplayName)
            .ToList();
        var note = providers.Count == 1 && string.Equals(providers[0], "Demo catalog", StringComparison.Ordinal)
            ? "Search used the Secret Base demo catalog only. This is not Spotify/YouTube API playback."
            : "Playback uses Secret Base music providers only. This is not Spotify/YouTube API playback.";
        var lines = result.Tracks.Take(8).Select(t => $"{t.Id}: {t.Title} ({t.Artist})").ToList();
        var body = lines.Count == 0
            ? "No tracks found. " + note
            : string.Join('\n', lines) + "\n" + note;
        return AssistantToolResult.Ok(
            body,
            activity: "Music ✓",
            activityDomain: AssistantActivityDomains.Music);
    }

    private AssistantToolResult MusicGetState()
    {
        if (_context is not null)
        {
            var state = _context.GetMusicState();
            var body =
                $"search={state.SearchAvailable}; playback={state.PlaybackAvailable}; demoCatalog={state.UsesDemoCatalog}; "
                + $"playing={state.IsPlaying}; track={state.CurrentTrackTitle ?? "(none)"}; note={state.Note}";
            return AssistantToolResult.Ok(
                body,
                activity: "Music ✓",
                activityDomain: AssistantActivityDomains.Music);
        }

        if (_music is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Music);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        var playback = _music.MusicService.GetPlaybackProvider();
        var demo = _music.MusicService.Providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .All(p => string.Equals(p.DisplayName, "Demo catalog", StringComparison.Ordinal));
        var body =
            $"search={caps.HasFlag(MusicProviderCapabilities.Search)}; "
            + $"playback={caps.HasFlag(MusicProviderCapabilities.Playback)}; demoCatalog={demo}; "
            + $"playing={playback?.IsPlaying ?? false}; track={playback?.CurrentTrack?.Title ?? "(none)"}; "
            + "note=Demo catalog is not Spotify/YouTube API playback.";
        return AssistantToolResult.Ok(
            body,
            activity: "Music ✓",
            activityDomain: AssistantActivityDomains.Music);
    }

    private async Task<AssistantToolResult> MusicPlayAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Music);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "track_id", required: true, out var trackId, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Music);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Playback))
        {
            return AssistantToolResult.Fail(
                "Music playback is not available. The demo catalog may search, but no playback-capable provider is configured. Secret Base does not invent Spotify playback.",
                activityDomain: AssistantActivityDomains.Music);
        }

        var result = await _music.ExecuteAsync(MusicCommand.PlayTrackById(trackId, providerId: string.Empty), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Music);
        }

        var title = result.CurrentTrack?.Title ?? trackId;
        return AssistantToolResult.Ok(
            $"Playing {title} in the Secret Base music catalog (demo/local providers only).",
            activity: "Music ✓",
            activityDomain: AssistantActivityDomains.Music);
    }

    private static string FormatEvents(IReadOnlyList<CalendarEvent> events, string window)
    {
        if (events.Count == 0)
        {
            return $"No events {window}.";
        }

        var lines = events.Take(12).Select(e =>
        {
            var when = e.IsAllDay ? e.Start.ToString("yyyy-MM-dd") : e.Start.ToString("yyyy-MM-dd HH:mm");
            return $"{when} {e.Title}";
        });
        return $"{events.Count} event(s) {window}:\n" + string.Join('\n', lines);
    }
}
