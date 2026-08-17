using System.Text.Json;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Routes registered tools to existing Command services. Never launches OS processes.
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

    public AssistantToolExecutor(
        IAiToolRegistry registry,
        CalendarCommandService? calendar = null,
        CreativeCommandService? creative = null,
        AiCommandService? ai = null,
        IntegrationCommandService? integration = null,
        AppCommandService? apps = null,
        MusicCommandService? music = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _calendar = calendar;
        _creative = creative;
        _ai = ai;
        _integration = integration;
        _apps = apps;
        _music = music;
    }

    public async Task<AssistantToolResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (_registry.Find(toolName) is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryParseObject(argumentsJson, out var root, out var parseError))
        {
            return AssistantToolResult.Fail(parseError);
        }

        return toolName switch
        {
            AssistantToolNames.CalendarGetToday => await CalendarTodayAsync(cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CalendarGetUpcoming => await CalendarUpcomingAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CreativeListProjects => CreativeList(),
            AssistantToolNames.CreativeOpenProject => CreativeOpen(root),
            AssistantToolNames.CursorOpenProject => CursorOpen(root),
            AssistantToolNames.IntegrationOpen => await IntegrationOpenAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.AppsList => AppsList(),
            AssistantToolNames.AppsOpen => AppsOpen(root),
            AssistantToolNames.MusicSearch => await MusicSearchAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.MusicPlay => await MusicPlayAsync(root, cancellationToken).ConfigureAwait(false),
            _ => AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable)
        };
    }

    private async Task<AssistantToolResult> CalendarTodayAsync(CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, "Calendar is not wired.");
        }

        var result = await _calendar.ExecuteAsync(CalendarCommand.GetTodayEvents(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, "today"),
            activity: "Read today's calendar");
    }

    private async Task<AssistantToolResult> CalendarUpcomingAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, "Calendar is not wired.");
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "days", 7, 1, 14, out var days, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var result = await _calendar.ExecuteAsync(CalendarCommand.GetUpcoming(days), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, $"next {days} day(s)"),
            activity: "Read upcoming calendar");
    }

    private AssistantToolResult CreativeList()
    {
        if (_creative is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, "Creative is not wired.");
        }

        var result = _creative.Execute(CreativeCommand.SearchProjects(null));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        var lines = result.Projects.Select(p => $"{p.Id}: {p.Name}").ToList();
        var body = lines.Count == 0 ? "No Creative Projects are registered." : string.Join('\n', lines);
        return AssistantToolResult.Ok(body, activity: "Listed Creative Projects");
    }

    private AssistantToolResult CreativeOpen(JsonElement root)
    {
        if (_creative is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var result = _creative.Execute(CreativeCommand.OpenCreativeProject(id));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        var name = result.Project?.Name ?? id;
        return AssistantToolResult.Ok(
            $"Opened Creative Project dashboard for {name}.",
            activity: $"Opened project {name}");
    }

    private AssistantToolResult CursorOpen(JsonElement root)
    {
        if (_ai is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var result = _ai.Execute(AiCommand.OpenProjectInCursor(id));
        if (!result.Succeeded)
        {
            var message = result.OfferCursorWebsiteFallback
                ? AssistantUserMessages.CursorOpenFailed
                : (result.ErrorMessage ?? AssistantUserMessages.CursorOpenFailed);
            return AssistantToolResult.Fail(message, activity: "Cursor launch was not available");
        }

        return AssistantToolResult.Ok(
            "Host may open this project in Cursor.",
            activity: "Requested Cursor open",
            shouldOpenCursorAtFolder: result.ShouldOpenCursorAtFolder,
            cursorFolderPath: result.FolderPath);
    }

    private async Task<AssistantToolResult> IntegrationOpenAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_integration is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "target", required: true, out var target, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var commandId = target.ToLowerInvariant() switch
        {
            "classroom" => IntegrationCommandIds.ClassroomOpen,
            "calendar" => IntegrationCommandIds.CalendarOpen,
            _ => null
        };
        if (commandId is null)
        {
            return AssistantToolResult.Fail("Unknown integration target. Use classroom or calendar.");
        }

        var result = await _integration.ExecuteAsync(
            new IntegrationRequest { CommandId = commandId },
            cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        return AssistantToolResult.Ok(
            $"Open {target} at {result.LaunchTarget}.",
            activity: $"Open {target}",
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private AssistantToolResult AppsList()
    {
        if (_apps is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        var result = _apps.Execute(AppCommand.ListApps());
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        var lines = result.Apps.Select(a => $"{a.Id}: {a.Name}").ToList();
        var body = lines.Count == 0 ? "No apps are registered." : string.Join('\n', lines);
        return AssistantToolResult.Ok(body, activity: "Listed My Apps");
    }

    private AssistantToolResult AppsOpen(JsonElement root)
    {
        if (_apps is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "app_id", required: true, out var id, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var result = _apps.Execute(AppCommand.OpenApp(id));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        return AssistantToolResult.Ok(
            $"Host may launch {result.App?.Name ?? id}.",
            activity: $"Launch {result.App?.Name ?? id}",
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private async Task<AssistantToolResult> MusicSearchAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "query", required: true, out var query, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Search))
        {
            return AssistantToolResult.Fail("Music search is not available. No search-capable provider is configured.");
        }

        var result = await _music.ExecuteAsync(MusicCommand.SearchTrack(query), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
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
        return AssistantToolResult.Ok(body, activity: "Searched music catalog");
    }

    private async Task<AssistantToolResult> MusicPlayAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "track_id", required: true, out var trackId, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Playback))
        {
            return AssistantToolResult.Fail(
                "Music playback is not available. The demo catalog may search, but no playback-capable provider is configured. Secret Base does not invent Spotify playback.");
        }

        var result = await _music.ExecuteAsync(MusicCommand.PlayTrackById(trackId, providerId: string.Empty), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable);
        }

        var title = result.CurrentTrack?.Title ?? trackId;
        return AssistantToolResult.Ok(
            $"Playing {title} in the Secret Base music catalog (demo/local providers only).",
            activity: $"Play {title}");
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
