using System.Text.Json;
using SecretBase.Core.Activity;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Capture;
using SecretBase.Core.Commands;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Integration;
using SecretBase.Core.Memory;
using SecretBase.Core.Music;
using SecretBase.Core.Privacy;
using SecretBase.Core.Search;
using SecretBase.Core.Timeline;
using SecretBase.Core.Attention;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

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
    private readonly WorkspaceCommandService? _workspace;
    private readonly IIntegrationMemory? _integrations;
    private readonly IBaseExperienceServices? _base;

    public AssistantToolExecutor(
        IAiToolRegistry registry,
        CalendarCommandService? calendar = null,
        CreativeCommandService? creative = null,
        AiCommandService? ai = null,
        IntegrationCommandService? integration = null,
        AppCommandService? apps = null,
        MusicCommandService? music = null,
        IAssistantContextService? context = null,
        WorkspaceCommandService? workspace = null,
        IIntegrationMemory? integrations = null,
        IBaseExperienceServices? baseExperience = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _calendar = calendar;
        _creative = creative;
        _ai = ai;
        _integration = integration;
        _apps = apps;
        _music = music;
        _context = context;
        _workspace = workspace;
        _integrations = integrations;
        _base = baseExperience;
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
            AssistantToolNames.CalendarAddEvent => await CalendarAddAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CalendarRememberUsual => await CalendarRememberUsualAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.CalendarApplyUsual => await CalendarApplyUsualAsync(cancellationToken).ConfigureAwait(false),
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
            AssistantToolNames.ProjectRecommend => await ProjectRecommendAsync(cancellationToken).ConfigureAwait(false),
            AssistantToolNames.ScheduleRecommend => await ScheduleRecommendAsync(cancellationToken).ConfigureAwait(false),
            AssistantToolNames.MusicRecommend => await MusicRecommendAsync(root, cancellationToken).ConfigureAwait(false),
            AssistantToolNames.WorkspaceOpenNamed => WorkspaceOpen(root),
            AssistantToolNames.WorkspaceRemove => WorkspaceRemove(root),
            AssistantToolNames.FilesDelete => WorkspaceRemove(root),
            AssistantToolNames.WorkspacePrepare => WorkspacePrepare(root),
            AssistantToolNames.WorkspaceContinue => WorkspaceContinue(root),
            AssistantToolNames.TodoList => TodoList(),
            AssistantToolNames.TodoAdd => TodoAdd(root),
            AssistantToolNames.FocusStart => FocusStart(root),
            AssistantToolNames.FilesSuggestCleanup => FilesSuggestCleanup(),
            AssistantToolNames.MemoryRecall => MemoryRecall(root),
            AssistantToolNames.MemoryRemember => MemoryRemember(root),
            AssistantToolNames.ActivityRecent => ActivityRecent(),
            AssistantToolNames.SearchBase => SearchBase(root),
            AssistantToolNames.UserState => UserState(),
            AssistantToolNames.SituationNow => SituationNow(),
            AssistantToolNames.SessionRecent => SessionRecent(),
            AssistantToolNames.DailyBriefing => DailyBriefingNow(),
            AssistantToolNames.CommandPalette => CommandPaletteNow(root),
            AssistantToolNames.QuickCapture => QuickCaptureNow(root),
            AssistantToolNames.IntentExplain => IntentExplainNow(),
            AssistantToolNames.ActivityTimeline => ActivityTimelineNow(),
            AssistantToolNames.PrivacyManifest => PrivacyNow(),
            AssistantToolNames.AttentionNow => AttentionNow(),
            AssistantToolNames.AutomationFeedback => AutomationFeedback(root),
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
        if (_base is not null)
        {
            var extras = new List<string>();
            var openTodos = _base.LoadTodos().Items.Where(item => !item.IsDone).Take(8).Select(item => item.Title).ToList();
            extras.Add("Todos: " + (openTodos.Count == 0 ? "(none)" : string.Join(", ", openTodos)));
            extras.Add("Focus: " + _base.Focus.Current.StatusLine(_base.Now));
            if (_base.CurrentWorkspace is not null)
            {
                extras.Add("Workspace:\n" + WorkspacePreparer.FormatCard(_base.CurrentWorkspace));
            }

            extras.Add(BaseAiCatalog.AllowedSlices);
            extras.Add("User state:\n" + FormatUserState(_base));
            extras.Add("Situation:\n" + _base.ComposeSituation().Format());
            extras.Add("Continuation:\n" + _base.Continuation().Format());

            text += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, extras);
        }
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
                AssistantUserMessages.CalendarFailed,
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
                AssistantUserMessages.CalendarFailed,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, $"next {days} day(s)"),
            activity: "Calendar ✓",
            activityDomain: AssistantActivityDomains.Calendar);
    }

    private async Task<AssistantToolResult> CalendarAddAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "title", required: true, out var title, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "hour", -1, -1, 23, out var hour, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "minute", 0, 0, 59, out var minute, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "duration_minutes", 60, 15, 480, out var duration, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        var result = await _calendar
            .ExecuteAsync(CalendarCommand.AddEvent(title, hour, minute, duration), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.CalendarFailed,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        var created = result.Events.FirstOrDefault();
        var when = created is null
            ? title
            : $"{created.Start:HH:mm} {created.Title}";
        return AssistantToolResult.Ok(
            $"Added local event: {when}. It appears in the Calendar widget (not pushed to Google).",
            activity: "Calendar ✓",
            activityDomain: AssistantActivityDomains.Calendar);
    }

    private async Task<AssistantToolResult> CalendarRememberUsualAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "title", required: true, out var title, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "hour", 9, 0, 23, out var hour, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "minute", 0, 0, 59, out var minute, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        if (!AssistantToolArgumentValidator.TryGetInt(root, "duration_minutes", 60, 15, 480, out var duration, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Calendar);
        }

        var result = await _calendar
            .ExecuteAsync(CalendarCommand.RememberUsual(title, hour, minute, duration), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.CalendarFailed,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        var slot = result.Usual;
        var label = slot is null ? title : $"{slot.Title} at {slot.Hour:00}:{slot.Minute:00}";
        return AssistantToolResult.Ok(
            $"Remembered usual schedule: {label}. Say the usual schedule to apply it to today.",
            activity: "Calendar ✓",
            activityDomain: AssistantActivityDomains.Calendar);
    }

    private async Task<AssistantToolResult> CalendarApplyUsualAsync(CancellationToken cancellationToken)
    {
        if (_calendar is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        var result = await _calendar.ExecuteAsync(CalendarCommand.ApplyUsual(), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.CalendarFailed,
                activityDomain: AssistantActivityDomains.Calendar);
        }

        return AssistantToolResult.Ok(
            FormatEvents(result.Events, "usual schedule applied today")
            + " Events are local Secret Base agenda items (Calendar widget), not Google writes.",
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

        var projectName = id;
        return AssistantToolResult.Ok(
            $"Cursor open prepared for project {projectName}. Host may launch Cursor at the registered folder. Report success only after Host confirms.",
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

        if (string.Equals(target, "classroom", StringComparison.OrdinalIgnoreCase))
        {
            _integrations?.RememberOpened(
                IntegrationMemoryIds.Classroom,
                "Google Classroom",
                inAppExperience: false);
        }

        var inApp = string.Equals(target, "calendar", StringComparison.OrdinalIgnoreCase);
        var note = inApp
            ? " Prefer the Calendar widget when Google Calendar is connected; browser Open is optional."
            : " Classroom has no API in Secret Base — the official site opens in the existing Web Widget.";
        return AssistantToolResult.Ok(
            $"Open {target} at {result.LaunchTarget}.{note}",
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
        var searchProviders = _music.MusicService.Providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .ToList();
        var demo = searchProviders.Count > 0
            && searchProviders.All(p => string.Equals(p.DisplayName, "Demo catalog", StringComparison.Ordinal));
        var fallbackBody =
            $"search={caps.HasFlag(MusicProviderCapabilities.Search)}; "
            + $"playback={caps.HasFlag(MusicProviderCapabilities.Playback)}; demoCatalog={demo}; "
            + $"playing={playback?.IsPlaying ?? false}; track={playback?.CurrentTrack?.Title ?? "(none)"}; "
            + "note=Demo catalog is not Spotify/YouTube API playback.";
        return AssistantToolResult.Ok(
            fallbackBody,
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

        AssistantToolArgumentValidator.TryGetString(root, "track_id", required: false, out var trackId, out var idError);
        if (!string.IsNullOrWhiteSpace(idError) && root.TryGetProperty("track_id", out _))
        {
            return AssistantToolResult.Fail(idError, activityDomain: AssistantActivityDomains.Music);
        }

        AssistantToolArgumentValidator.TryGetString(root, "query", required: false, out var query, out var queryError);
        if (!string.IsNullOrWhiteSpace(queryError) && root.TryGetProperty("query", out _))
        {
            return AssistantToolResult.Fail(queryError, activityDomain: AssistantActivityDomains.Music);
        }

        if (string.IsNullOrWhiteSpace(trackId) && !string.IsNullOrWhiteSpace(query))
        {
            var search = await _music.ExecuteAsync(MusicCommand.SearchTrack(query), cancellationToken)
                .ConfigureAwait(false);
            if (!search.Succeeded || search.Tracks.Count == 0)
            {
                return AssistantToolResult.Fail(
                    search.ErrorMessage ?? "No matching track in the Secret Base music catalog.",
                    activityDomain: AssistantActivityDomains.Music);
            }

            trackId = search.Tracks[0].Id;
        }

        if (string.IsNullOrWhiteSpace(trackId))
        {
            return AssistantToolResult.Fail(
                "Provide track_id or query.",
                activityDomain: AssistantActivityDomains.Music);
        }

        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Playback))
        {
            return AssistantToolResult.Fail(
                "Music playback is not available. Connect Spotify in the Music widget to play inside Secret Base. Secret Base does not invent Spotify playback.",
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
        var spotify = _music.MusicService.Providers.Any(p =>
            string.Equals(p.ProviderId, "spotify", StringComparison.Ordinal)
            && p.AuthStatus == MusicAuthStatus.Connected);
        var where = spotify
            ? "Spotify via the Music widget"
            : "the Secret Base music catalog (demo/local providers only)";
        return AssistantToolResult.Ok(
            $"Playing {title} in {where}.",
            activity: "Music ✓",
            activityDomain: AssistantActivityDomains.Music);
    }

    private AssistantToolResult WorkspaceOpen(JsonElement root)
    {
        if (_workspace is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "name", required: true, out var name, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Workspace);
        }

        var result = _workspace.Execute(WorkspaceCommand.OpenNamed(name));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        return AssistantToolResult.Ok(
            result.Message ?? $"Opened {name}.",
            activity: "Workspace ✓",
            activityDomain: AssistantActivityDomains.Workspace,
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private AssistantToolResult WorkspaceRemove(JsonElement root)
    {
        if (_workspace is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.DiskDeleteRefused,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "name", required: true, out var name, out var error))
        {
            return AssistantToolResult.Fail(
                string.IsNullOrWhiteSpace(error) || error == AssistantUserMessages.ToolUnavailable
                    ? AssistantUserMessages.DiskDeleteRefused
                    : error,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        var result = _workspace.Execute(WorkspaceCommand.RemoveNamed(name));
        if (!result.Succeeded)
        {
            return AssistantToolResult.Fail(
                result.ErrorMessage ?? AssistantUserMessages.DiskDeleteRefused,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        return AssistantToolResult.Ok(
            result.Message ?? $"Removed {name} from Secret Base (disk files were not deleted).",
            activity: "Workspace ✓",
            activityDomain: AssistantActivityDomains.Workspace);
    }

    private async Task<AssistantToolResult> ProjectRecommendAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ProjectsFailed,
                activityDomain: AssistantActivityDomains.Suggest);
        }

        var snapshot = await _context.GetSnapshotAsync(AssistantContextScope.Creative, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot.Projects.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No Creative Projects are registered. Suggest the user add one in Creative Workspace. Do not invent projects.",
                activity: "Suggest ✓",
                activityDomain: AssistantActivityDomains.Suggest);
        }

        var favorite = snapshot.Projects.FirstOrDefault(p => p.IsFavorite);
        var recent = snapshot.RecentProjects.FirstOrDefault();
        var pick = favorite ?? recent ?? snapshot.Projects[0];
        var reason = favorite is not null
            ? "marked favorite"
            : recent is not null
                ? "opened recently"
                : "first registered project";
        var body =
            $"Candidate project: {pick.Id} ({pick.Name}) — {reason}. "
            + "Phrase as a suggestion from registered projects, not a life decision. "
            + "Do not open Cursor unless the user clearly asks.";
        return AssistantToolResult.Ok(
            body,
            activity: "Suggest ✓",
            activityDomain: AssistantActivityDomains.Suggest);
    }

    private async Task<AssistantToolResult> ScheduleRecommendAsync(CancellationToken cancellationToken)
    {
        if (_context is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Suggest);
        }

        var snapshot = await _context
            .GetSnapshotAsync(AssistantContextScope.Calendar | AssistantContextScope.Creative, cancellationToken)
            .ConfigureAwait(false);

        var lines = new List<string>
        {
            "Schedule recommendation (candidates only — do not assert what the user must do):"
        };

        if (snapshot.TodayEvents.Count == 0)
        {
            lines.Add("- Today: no registered calendar events.");
        }
        else
        {
            foreach (var e in snapshot.TodayEvents.Take(5))
            {
                var when = e.IsAllDay ? "all-day" : e.Start.ToString("HH:mm");
                lines.Add($"- Event: {when} {e.Title}");
            }
        }

        if (snapshot.FreeTimeSlots.Count > 0)
        {
            lines.Add("- Free windows: " + string.Join(", ", snapshot.FreeTimeSlots.Take(4).Select(s => s.ToString())));
        }

        if (snapshot.Projects.Count == 0)
        {
            lines.Add("- Projects: none registered.");
        }
        else
        {
            var pick = snapshot.RecentProjects.FirstOrDefault()
                       ?? snapshot.Projects.FirstOrDefault(p => p.IsFavorite)
                       ?? snapshot.Projects[0];
            lines.Add($"- Project candidate before/after events: {pick.Name} ({pick.Id}).");
        }

        lines.Add("Tell the user these are candidates from Calendar + Projects, not certainty about their life.");
        return AssistantToolResult.Ok(
            string.Join('\n', lines),
            activity: "Suggest ✓",
            activityDomain: AssistantActivityDomains.Suggest);
    }

    private async Task<AssistantToolResult> MusicRecommendAsync(JsonElement root, CancellationToken cancellationToken)
    {
        if (_music is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Suggest);
        }

        AssistantToolArgumentValidator.TryGetString(root, "query", required: false, out var query, out _);
        var q = string.IsNullOrWhiteSpace(query) ? "focus" : query!;
        var caps = _music.MusicService.AggregateCapabilities();
        if (!caps.HasFlag(MusicProviderCapabilities.Search))
        {
            return AssistantToolResult.Ok(
                "Music search is not available. Do not invent Spotify/YouTube recommendations.",
                activity: "Suggest ✓",
                activityDomain: AssistantActivityDomains.Suggest);
        }

        var result = await _music.ExecuteAsync(MusicCommand.SearchTrack(q), cancellationToken)
            .ConfigureAwait(false);
        if (!result.Succeeded || result.Tracks.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No music candidates found in the Secret Base catalog. Demo catalog only — not Spotify/YouTube API.",
                activity: "Suggest ✓",
                activityDomain: AssistantActivityDomains.Suggest);
        }

        var track = result.Tracks[0];
        var demo = _music.MusicService.Providers
            .Where(p => p.Capabilities.HasFlag(MusicProviderCapabilities.Search))
            .All(p => string.Equals(p.DisplayName, "Demo catalog", StringComparison.Ordinal));
        var note = demo
            ? "Suggestion uses the Secret Base demo catalog only. Do not claim Spotify/YouTube playback."
            : "Suggestion uses Secret Base music providers only.";
        return AssistantToolResult.Ok(
            $"Music candidate: {track.Id} — {track.Title} ({track.Artist}). {note} Do not call music_play unless the user asked to play.",
            activity: "Suggest ✓",
            activityDomain: AssistantActivityDomains.Suggest);
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

    private AssistantToolResult WorkspacePrepare(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        AssistantToolArgumentValidator.TryGetString(root, "intent", required: false, out var intent, out _);
        var session = WorkspacePreparer.Prepare(
            intent ?? string.Empty,
            _base.ListProjects(),
            _base.ListApps(),
            _base.LoadTodos(),
            _base.ListUpcomingEvents(),
            _base.Now,
            _base.Memory.Recall(_base.Now));
        _base.RememberPreparedWorkspace(session);
        return AssistantToolResult.Ok(
            WorkspacePreparer.FormatCard(session),
            activity: "Workspace prepared",
            activityDomain: AssistantActivityDomains.Workspace);
    }

    private AssistantToolResult WorkspaceContinue(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        AssistantToolArgumentValidator.TryGetString(root, "intent", required: false, out var intent, out _);
        var session = _base.CurrentWorkspace
                      ?? WorkspacePreparer.Prepare(
                          intent ?? string.Empty,
                          _base.ListProjects(),
                          _base.ListApps(),
                          _base.LoadTodos(),
                          _base.ListUpcomingEvents(),
                          _base.Now,
                          _base.Memory.Recall(_base.Now));
        _base.RememberPreparedWorkspace(session);

        if (string.IsNullOrWhiteSpace(session.ProjectId) || _creative is null)
        {
            return AssistantToolResult.Ok(
                WorkspacePreparer.FormatCard(session)
                + Environment.NewLine
                + Environment.NewLine
                + "No registered project to open. Register a Creative Project first. Apps were not launched.",
                activity: "Workspace ready (no project)",
                activityDomain: AssistantActivityDomains.Workspace);
        }

        var opened = _creative.Execute(CreativeCommand.OpenCreativeProject(session.ProjectId));
        if (!opened.Succeeded)
        {
            return AssistantToolResult.Fail(
                opened.ErrorMessage ?? AssistantUserMessages.ProjectsFailed,
                activityDomain: AssistantActivityDomains.Workspace);
        }

        _base.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Project opened",
            ProjectName = opened.Project?.Name ?? session.ProjectName,
            At = _base.Now
        });

        var card = WorkspacePreparer.FormatCard(session)
                   + Environment.NewLine
                   + Environment.NewLine
                   + $"Opened project '{opened.Project?.Name ?? session.ProjectName}'. "
                   + "Registered apps were listed, not auto-launched. Git was not run.";
        return AssistantToolResult.Ok(
            card,
            activity: "Workspace continue",
            activityDomain: AssistantActivityDomains.Workspace);
    }

    private AssistantToolResult TodoList()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Todo);
        }

        var items = _base.LoadTodos().Items;
        if (items.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No local todos.",
                activity: "Todo ✓",
                activityDomain: AssistantActivityDomains.Todo);
        }

        var lines = items.Take(20).Select(item =>
            (item.IsDone ? "[x] " : "[ ] ") + item.Title);
        return AssistantToolResult.Ok(
            string.Join(Environment.NewLine, lines),
            activity: "Todo ✓",
            activityDomain: AssistantActivityDomains.Todo);
    }

    private AssistantToolResult TodoAdd(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Todo);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "title", required: true, out var title, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Todo);
        }

        var list = _base.LoadTodos();
        list.Items.Add(TodoItem.Create(title));
        _base.SaveTodos(list);
        _base.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.TodoCreated,
            Title = title,
            At = _base.Now
        });
        return AssistantToolResult.Ok(
            $"Added todo: {title}",
            activity: "Todo ✓",
            activityDomain: AssistantActivityDomains.Todo);
    }

    private AssistantToolResult FocusStart(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Focus);
        }

        AssistantToolArgumentValidator.TryGetInt(root, "minutes", 25, 5, 90, out var minutes, out _);
        AssistantToolArgumentValidator.TryGetString(root, "label", required: false, out var label, out _);
        var session = _base.Focus.Start(_base.Now, TimeSpan.FromMinutes(minutes), label);
        _base.Sessions.StartOrContinue(
            _base.Now,
            _base.CurrentWorkspace?.ProjectId,
            _base.CurrentWorkspace?.ProjectName,
            _base.CurrentWorkspace?.Title);
        _base.Sessions.Touch(session.Label, null, null, focusStarted: true);
        _base.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.FocusStarted,
            Title = session.Label,
            ProjectName = _base.CurrentWorkspace?.ProjectName,
            At = _base.Now
        });
        return AssistantToolResult.Ok(
            $"Started {session.Label} for {minutes} minutes. No apps were launched.",
            activity: "Focus ✓",
            activityDomain: AssistantActivityDomains.Focus);
    }

    private AssistantToolResult FilesSuggestCleanup()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Files);
        }

        var candidates = FileIntelligence.SuggestCleanup(_base.ListProjects(), _base.Now);
        return AssistantToolResult.Ok(
            FileIntelligence.FormatSuggestion(candidates),
            activity: "Files ✓",
            activityDomain: AssistantActivityDomains.Files);
    }

    private AssistantToolResult MemoryRecall(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Memory);
        }

        AssistantToolArgumentValidator.TryGetString(root, "query", required: false, out var query, out _);
        var items = string.IsNullOrWhiteSpace(query)
            ? _base.Memory.Recall(_base.Now, take: 12)
            : _base.Memory.RecallRanked(_base.Now, query, _base.ComposeUserState().CurrentProjectName, 8);
        if (items.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No durable memories yet.",
                activity: "Memory ✓",
                activityDomain: AssistantActivityDomains.Memory);
        }

        var lines = items.Select(item =>
            $"{item.Scope}: {item.Summary}"
            + (string.IsNullOrWhiteSpace(item.ProjectName) ? string.Empty : $" ({item.ProjectName})"));
        return AssistantToolResult.Ok(
            string.Join(Environment.NewLine, lines),
            activity: "Memory ✓",
            activityDomain: AssistantActivityDomains.Memory);
    }

    private AssistantToolResult MemoryRemember(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Memory);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "key", required: true, out var key, out var error)
            || !AssistantToolArgumentValidator.TryGetString(root, "summary", required: true, out var summary, out error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Memory);
        }

        AssistantToolArgumentValidator.TryGetString(root, "detail", required: false, out var detail, out _);
        try
        {
            _base.Memory.Remember(new MemoryEntry
            {
                Scope = MemoryScope.Decision,
                Key = key,
                Summary = summary,
                Detail = detail,
                Source = "assistant",
                Confidence = 0.7,
                Importance = MemoryImportance.Normal,
                ProjectId = _base.CurrentWorkspace?.ProjectId,
                ProjectName = _base.CurrentWorkspace?.ProjectName,
                CreatedAt = _base.Now,
                LastAccessedAt = _base.Now,
                ExpiresAt = MemoryPolicy.DefaultExpiry(MemoryScope.Decision, _base.Now)
            });
        }
        catch (InvalidOperationException)
        {
            return AssistantToolResult.Fail(
                "Memory refused a sensitive or path-like payload.",
                activityDomain: AssistantActivityDomains.Memory);
        }

        return AssistantToolResult.Ok(
            "Remembered: " + summary,
            activity: "Memory ✓",
            activityDomain: AssistantActivityDomains.Memory);
    }

    private AssistantToolResult ActivityRecent()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Activity);
        }

        var meaningful = _base.Activity.Meaningful(_base.Now);
        if (meaningful.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No recent activity in Secret Base.",
                activity: "Activity ✓",
                activityDomain: AssistantActivityDomains.Activity);
        }

        var lines = meaningful.TakeLast(8).Select(item =>
            $"{item.Title} — {item.Summary}");
        return AssistantToolResult.Ok(
            string.Join(Environment.NewLine, lines),
            activity: "Activity ✓",
            activityDomain: AssistantActivityDomains.Activity);
    }

    private AssistantToolResult SearchBase(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Search);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "query", required: true, out var query, out var error))
        {
            return AssistantToolResult.Fail(error, activityDomain: AssistantActivityDomains.Search);
        }

        return AssistantToolResult.Ok(
            BaseSearch.Format(_base.Search(query)),
            activity: "Search ✓",
            activityDomain: AssistantActivityDomains.Search);
    }

    private AssistantToolResult UserState()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.State);
        }

        return AssistantToolResult.Ok(
            FormatUserState(_base),
            activity: "State ✓",
            activityDomain: AssistantActivityDomains.State);
    }

    private AssistantToolResult AutomationFeedback(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Automation);
        }

        var accepted = root.TryGetProperty("accepted", out var el)
                       && el.ValueKind is JsonValueKind.True;
        if (!root.TryGetProperty("accepted", out _))
        {
            return AssistantToolResult.Fail(
                "accepted is required.",
                activityDomain: AssistantActivityDomains.Automation);
        }

        _base.RecordFeedback(accepted);
        return AssistantToolResult.Ok(
            accepted ? "Recorded accept." : "Recorded dismiss.",
            activity: "Automation ✓",
            activityDomain: AssistantActivityDomains.Automation);
    }

    private static string FormatUserState(IBaseExperienceServices services)
    {
        var state = services.ComposeUserState();
        var intent = services.DetectIntent();
        var continuation = services.Continuation();
        return string.Join(
            Environment.NewLine,
            [
                $"{state.Greeting} · confidence {state.Confidence:0.00}",
                "Project: " + (state.CurrentProjectName ?? "(none)"),
                "Workspace: " + (state.CurrentWorkspaceTitle ?? "(none)"),
                "Focus: " + state.FocusLine,
                "Calendar: " + (state.CurrentCalendarTitle ?? state.UpcomingCalendarTitle ?? "(none)"),
                "Todo: " + (state.ActiveTodo ?? "(none)"),
                "Activity: " + (string.IsNullOrWhiteSpace(state.RecentActivityLine) ? "(none)" : state.RecentActivityLine),
                $"Intent: {intent.Kind} ({intent.Confidence:0.00}) — {intent.Rationale}",
                intent.Evidence.Count == 0
                    ? "Evidence: (none)"
                    : "Evidence: " + string.Join("; ", intent.Evidence.Take(4)),
                continuation.Format(),
                BaseAiCatalog.AllowedSlices,
                "Intent is not an action. Confirmation still applies before launch."
            ]);
    }

    private AssistantToolResult SituationNow()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Situation);
        }

        return AssistantToolResult.Ok(
            _base.ComposeSituation().Format()
            + Environment.NewLine
            + BaseAiCatalog.Forbidden,
            activity: "Situation ✓",
            activityDomain: AssistantActivityDomains.Situation);
    }

    private AssistantToolResult SessionRecent()
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(
                AssistantUserMessages.ToolUnavailable,
                activityDomain: AssistantActivityDomains.Session);
        }

        var sessions = _base.Sessions.Recent(8);
        if (sessions.Count == 0)
        {
            return AssistantToolResult.Ok(
                "No work sessions recorded yet.",
                activity: "Session ✓",
                activityDomain: AssistantActivityDomains.Session);
        }

        var lines = sessions.Select(item =>
            $"{item.StartedAt:HH:mm} {item.Summary}"
            + (item.EndedAt is null ? " (open)" : string.Empty));
        return AssistantToolResult.Ok(
            string.Join(Environment.NewLine, lines),
            activity: "Session ✓",
            activityDomain: AssistantActivityDomains.Session);
    }

    private AssistantToolResult DailyBriefingNow()
        => _base is null
            ? AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.State)
            : AssistantToolResult.Ok(_base.Briefing().Format(), activity: "Briefing ✓", activityDomain: AssistantActivityDomains.State);

    private AssistantToolResult CommandPaletteNow(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.Search);
        }

        AssistantToolArgumentValidator.TryGetString(root, "query", required: false, out var query, out _);
        var items = _base.Palette(query ?? string.Empty);
        var body = string.Join(
            Environment.NewLine,
            items.Select(item => $"{item.Title} — {item.Subtitle}"));
        return AssistantToolResult.Ok(
            string.IsNullOrWhiteSpace(body) ? "Nothing ranked yet." : body,
            activity: "Palette ✓",
            activityDomain: AssistantActivityDomains.Search);
    }

    private AssistantToolResult QuickCaptureNow(JsonElement root)
    {
        if (_base is null)
        {
            return AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.Memory);
        }

        if (!AssistantToolArgumentValidator.TryGetString(root, "text", required: true, out var text, out var error))
        {
            return AssistantToolResult.Fail(error);
        }

        AssistantToolArgumentValidator.TryGetString(root, "kind", required: false, out var kind, out _);
        var draft = _base.ClassifyCapture(text);
        CaptureDestination? force = kind?.Trim().ToLowerInvariant() switch
        {
            "idea" => CaptureDestination.Idea,
            "todo" => CaptureDestination.Todo,
            "note" => CaptureDestination.Note,
            "memory" => CaptureDestination.Memory,
            "project" => CaptureDestination.Project,
            _ => null
        };
        var saved = _base.CommitCapture(draft, force);
        return saved is null
            ? AssistantToolResult.Fail("Capture was empty or contained a secret.", activityDomain: AssistantActivityDomains.Memory)
            : AssistantToolResult.Ok(
                $"Saved as {saved.Kind}: {saved.Summary}",
                activity: "Capture ✓",
                activityDomain: AssistantActivityDomains.Memory);
    }

    private AssistantToolResult IntentExplainNow()
        => _base is null
            ? AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.Situation)
            : AssistantToolResult.Ok(_base.ExplainIntent(), activity: "Explain ✓", activityDomain: AssistantActivityDomains.Situation);

    private AssistantToolResult ActivityTimelineNow()
        => _base is null
            ? AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.Activity)
            : AssistantToolResult.Ok(
                ActivityTimeline.Format(_base.Timeline()),
                activity: "Timeline ✓",
                activityDomain: AssistantActivityDomains.Activity);

    private AssistantToolResult PrivacyNow()
        => AssistantToolResult.Ok(
            _base?.Privacy() ?? PrivacyManifest.Format(),
            activity: "Privacy ✓",
            activityDomain: AssistantActivityDomains.Context);

    private AssistantToolResult AttentionNow()
        => _base is null
            ? AssistantToolResult.Fail(AssistantUserMessages.ToolUnavailable, activityDomain: AssistantActivityDomains.State)
            : AssistantToolResult.Ok(
                AttentionCenter.Format(_base.Attention()),
                activity: "Attention ✓",
                activityDomain: AssistantActivityDomains.State);
}
