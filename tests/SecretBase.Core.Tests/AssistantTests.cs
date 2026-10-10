using System.Text.Json;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;
using SecretBase.Core.Security;
using SecretBase.Core.Time;
using SecretBase.Core.Workspace;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Core.Widgets.Assistant;

namespace SecretBase.Core.Tests;

public class AssistantToolRegistryTests
{
    [Fact]
    public void BuiltinRegistry_ListsMvpTools_WithConfirmationPolicy()
    {
        var registry = BuiltinAssistantToolRegistry.Instance;
        Assert.Equal(30, registry.Tools.Count);
        Assert.NotNull(registry.Find(AssistantToolNames.CalendarAddEvent));
        Assert.NotNull(registry.Find(AssistantToolNames.CalendarApplyUsual));
        Assert.NotNull(registry.Find(AssistantToolNames.WorkspaceOpenNamed));
        Assert.NotNull(registry.Find(AssistantToolNames.WorkspacePrepare));
        Assert.NotNull(registry.Find(AssistantToolNames.WorkspaceContinue));
        Assert.NotNull(registry.Find(AssistantToolNames.FilesSuggestCleanup));
        Assert.Equal(AssistantToolCapability.SafeAuto, registry.Find(AssistantToolNames.WorkspacePrepare)!.Capability);
        Assert.Equal(AssistantToolCapability.SafeAuto, registry.Find(AssistantToolNames.FocusStart)!.Capability);
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.WorkspacePrepare)!));
        Assert.True(registry.Find(AssistantToolNames.WorkspaceContinue)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.TodoAdd)!.RequiresConfirmation);
        Assert.NotNull(registry.Find(AssistantToolNames.AssistantGetContext));
        Assert.NotNull(registry.Find(AssistantToolNames.CalendarGetToday));
        Assert.NotNull(registry.Find("CALENDAR_GET_UPCOMING"));
        Assert.NotNull(registry.Find(AssistantToolNames.CreativeGetProject));
        Assert.NotNull(registry.Find(AssistantToolNames.MusicGetState));
        Assert.NotNull(registry.Find(AssistantToolNames.ProjectRecommend));
        Assert.NotNull(registry.Find(AssistantToolNames.ScheduleRecommend));
        Assert.NotNull(registry.Find(AssistantToolNames.MusicRecommend));
        Assert.Null(registry.Find("shell.run"));
        Assert.Null(registry.Find("calendar.get_today"));

        Assert.Equal(AssistantToolCapability.ReadOnly, registry.Find(AssistantToolNames.AssistantGetContext)!.Capability);
        Assert.Equal(AssistantToolCapability.ReadOnly, registry.Find(AssistantToolNames.CalendarGetToday)!.Capability);
        Assert.Equal(AssistantToolCapability.ReadOnly, registry.Find(AssistantToolNames.CreativeGetProject)!.Capability);
        Assert.Equal(AssistantToolCapability.ReadOnly, registry.Find(AssistantToolNames.MusicGetState)!.Capability);
        Assert.Equal(AssistantToolCapability.Suggest, registry.Find(AssistantToolNames.ProjectRecommend)!.Capability);
        Assert.Equal(AssistantToolCapability.Suggest, registry.Find(AssistantToolNames.ScheduleRecommend)!.Capability);
        Assert.Equal(AssistantToolCapability.Suggest, registry.Find(AssistantToolNames.MusicRecommend)!.Capability);
        Assert.False(registry.Find(AssistantToolNames.CalendarGetToday)!.RequiresConfirmation);
        Assert.False(registry.Find(AssistantToolNames.CalendarGetUpcoming)!.RequiresConfirmation);
        Assert.False(registry.Find(AssistantToolNames.CreativeListProjects)!.RequiresConfirmation);
        Assert.False(registry.Find(AssistantToolNames.AppsList)!.RequiresConfirmation);
        Assert.False(registry.Find(AssistantToolNames.MusicSearch)!.RequiresConfirmation);
        Assert.False(registry.Find(AssistantToolNames.ProjectRecommend)!.RequiresConfirmation);

        Assert.Equal(AssistantToolCapability.RequiresConfirmation, registry.Find(AssistantToolNames.CreativeOpenProject)!.Capability);
        Assert.Equal(AssistantToolCapability.RequiresConfirmation, registry.Find(AssistantToolNames.CursorOpenProject)!.Capability);
        Assert.True(registry.Find(AssistantToolNames.CreativeOpenProject)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.CursorOpenProject)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.IntegrationOpen)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.AppsOpen)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.MusicPlay)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.CalendarAddEvent)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.CalendarApplyUsual)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.WorkspaceOpenNamed)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.WorkspaceRemove)!.RequiresConfirmation);
        Assert.True(registry.Find(AssistantToolNames.FilesDelete)!.RequiresConfirmation);

        Assert.Equal(ActionPrivilege.Observation, registry.Find(AssistantToolNames.CalendarGetToday)!.RiskLevel);
        Assert.Equal(ActionPrivilege.UserConfirmationRequired, registry.Find(AssistantToolNames.CursorOpenProject)!.RiskLevel);
        Assert.DoesNotContain(registry.Tools, t => t.Name.Contains('.', StringComparison.Ordinal));
        Assert.DoesNotContain(registry.Tools, t => t.Capability == AssistantToolCapability.HostAction);
    }
}

public class AssistantToolArgumentValidatorTests
{
    [Fact]
    public void ParsesObject_AndRejectsPathsSchemesCommands()
    {
        Assert.True(AssistantToolArgumentValidator.TryParseObject("""{"project_id":"abc"}""", out var root, out _));
        Assert.True(AssistantToolArgumentValidator.TryGetString(root, "project_id", required: true, out var id, out _));
        Assert.Equal("abc", id);

        Assert.False(AssistantToolArgumentValidator.TryGetString(
            JsonDocument.Parse("""{"project_id":"C:\\Windows\\notepad.exe --help"}""").RootElement,
            "project_id",
            required: true,
            out _,
            out var exeError));
        Assert.Equal(AssistantUserMessages.ToolUnavailable, exeError);

        Assert.False(AssistantToolArgumentValidator.TryGetString(
            JsonDocument.Parse("""{"query":"https://evil.example"}""").RootElement,
            "query",
            required: true,
            out _,
            out var urlError));
        Assert.Equal(AssistantUserMessages.ToolUnavailable, urlError);

        Assert.False(AssistantToolArgumentValidator.TryParseObject("[1]", out _, out var arrayError));
        Assert.Contains("JSON object", arrayError, StringComparison.OrdinalIgnoreCase);

        Assert.True(AssistantToolArgumentValidator.TryGetInt(
            JsonDocument.Parse("""{"days":99}""").RootElement,
            "days",
            7,
            1,
            14,
            out var days,
            out _));
        Assert.Equal(14, days);
    }
}

public class AssistantConfirmationPolicyTests
{
    [Fact]
    public void PromptsLaunchTools_AndLeavesReadsUnconfirmed()
    {
        var registry = BuiltinAssistantToolRegistry.Instance;
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.AppsList)!));
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.AssistantGetContext)!));
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.ScheduleRecommend)!));
        Assert.False(AssistantConfirmationPolicy.RequiresConfirmation(registry.Find(AssistantToolNames.AppsList)!));
        Assert.True(AssistantConfirmationPolicy.RequiresConfirmation(registry.Find(AssistantToolNames.CursorOpenProject)!));
        Assert.False(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.CursorOpenProject)!));

        var prompt = AssistantConfirmationPolicy.Prompt(
            AssistantToolNames.CursorOpenProject,
            """{"project_id":"pokemon"}""");
        Assert.Contains("pokemon", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cursor", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostAction_IsNeverAutoExecutable()
    {
        var hostOnly = new AssistantToolDefinition
        {
            Name = "host_only",
            Description = "reserved",
            Capability = AssistantToolCapability.HostAction,
            RiskLevel = ActionPrivilege.RestrictedAction
        };
        Assert.True(AssistantConfirmationPolicy.IsHostActionOnly(hostOnly));
        Assert.False(AssistantConfirmationPolicy.CanAutoExecute(hostOnly));
    }
}

public class AssistantMessageModelTests
{
    [Fact]
    public void ProviderResponses_CarryStatusWithoutSecrets()
    {
        var text = AiProviderResponse.Text("hello");
        Assert.Equal(AiProviderStatus.Ok, text.Status);
        Assert.Equal("hello", text.Content);

        var tools = AiProviderResponse.Tools(
        [
            new AiToolCall { Id = "c1", Name = AssistantToolNames.AppsList, ArgumentsJson = "{}" }
        ]);
        Assert.Single(tools.ToolCalls);
        Assert.Equal(AssistantToolNames.AppsList, tools.ToolCalls[0].Name);

        var missing = AiProviderResponse.NotConfigured();
        Assert.Equal(AiProviderStatus.NotConfigured, missing.Status);
        Assert.Equal(AssistantUserMessages.NotConfigured, missing.ErrorMessage);
        Assert.DoesNotContain("sk-", missing.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var down = AiProviderResponse.Unavailable();
        Assert.Equal(AssistantUserMessages.Unavailable, down.ErrorMessage);
    }
}

public class AssistantSettingsTests
{
    [Fact]
    public void Migrator_FillsProviderAndModel_NeverIntroducesApiKey()
    {
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings
        {
            SchemaVersion = 0,
            ProviderId = " ",
            Model = ""
        });
        Assert.Equal(AssistantSettings.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(AssistantProviderIds.OpenAi, migrated.ProviderId);
        Assert.Equal(AssistantSettings.DefaultOpenAiModel, migrated.Model);
        Assert.Null(typeof(AssistantSettings).GetProperty("ApiKey"));
        Assert.Null(typeof(AssistantSettings).GetProperty("ApiToken"));

        var store = new MemoryAssistantSettingsStore();
        store.Save(new AssistantSettings { ProviderId = AssistantProviderIds.Gemini, Model = "gemini-test" });
        var loaded = store.LoadOrCreate();
        Assert.Equal(AssistantProviderIds.Gemini, loaded.ProviderId);
        Assert.Equal("gemini-test", loaded.Model);
    }
}

public class AssistantWidgetConfigurationTests
{
    [Fact]
    public void RoundTrip_HasNoSecretFields()
    {
        var config = AssistantWidgetConfiguration.CreateDefault();
        var dict = config.ToDictionary();
        var restored = AssistantWidgetConfiguration.FromDictionary(dict);
        Assert.Equal(AssistantWidgetConfiguration.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.DoesNotContain(dict.Keys, k => k.Contains("key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(dict.Keys, k => k.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(DesktopLayout.CreateDefault().Widgets, w => w.Type == WidgetTypes.Assistant);
    }
}

public class AssistantProviderStubTests
{
    [Fact]
    public async Task UnconfiguredAndUnavailable_DoNotCallTools()
    {
        var missing = new UnconfiguredAiProvider(AssistantProviderIds.OpenAi, "OpenAI");
        var missingReply = await missing.ChatAsync([], [], "gpt-4o-mini");
        Assert.Equal(AiProviderStatus.NotConfigured, missingReply.Status);

        var local = new UnavailableAiProvider(AssistantProviderIds.Local, "Local", "AI provider is unavailable. Local/Ollama is not implemented yet — select OpenAI.");
        var localReply = await local.ChatAsync([], [], "llama");
        Assert.Equal(AiProviderStatus.Unavailable, localReply.Status);
        Assert.Contains("unavailable", localReply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}

public class AssistantToolExecutorTests
{
    [Fact]
    public async Task RoutesRegisteredTools_ToExistingCommands_AndRejectsUnknown()
    {
        var day = new DateOnly(2026, 8, 17);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "東進",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
                    }
                ])
            ]),
            new AssistantFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));

        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Pokemon Damage Calculator",
            "calc",
            CreativeProjectType.Other,
            @"D:\src\pokemon",
            out var project,
            out _));

        var ai = new AiCommandService(
            AiWorkspaceWidgetConfiguration.CreateDefault(),
            projects,
            () => true);
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects,
            ai);

        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(apps.Apps.TryAdd(new CustomApp
        {
            Name = "DTM AI",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Tools\DtmAi.exe"
        }, out var app, out _));

        var music = new MusicCommandService(new MusicService());
        var integration = new IntegrationCommandService(calendar: calendar, apps: apps);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            calendar,
            creative,
            ai,
            integration,
            apps,
            music);

        var today = await executor.ExecuteAsync(AssistantToolNames.CalendarGetToday, "{}");
        Assert.True(today.Succeeded);
        Assert.Contains("東進", today.ContentForModel, StringComparison.Ordinal);

        var upcoming = await executor.ExecuteAsync(AssistantToolNames.CalendarGetUpcoming, """{"days":3}""");
        Assert.True(upcoming.Succeeded);

        var listed = await executor.ExecuteAsync(AssistantToolNames.CreativeListProjects, "{}");
        Assert.True(listed.Succeeded);
        Assert.Contains("Pokemon Damage Calculator", listed.ContentForModel, StringComparison.Ordinal);

        var opened = await executor.ExecuteAsync(
            AssistantToolNames.CreativeOpenProject,
            $$"""{"project_id":"{{project!.Id}}"}""");
        Assert.True(opened.Succeeded);

        var cursor = await executor.ExecuteAsync(
            AssistantToolNames.CursorOpenProject,
            $$"""{"project_id":"{{project.Id}}"}""");
        Assert.True(cursor.Succeeded);
        Assert.True(cursor.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\src\pokemon", cursor.CursorFolderPath);

        var classroom = await executor.ExecuteAsync(
            AssistantToolNames.IntegrationOpen,
            """{"target":"classroom"}""");
        Assert.True(classroom.Succeeded);
        Assert.True(classroom.ShouldLaunch);
        Assert.True(classroom.LaunchIsExternalLink);

        var appList = await executor.ExecuteAsync(AssistantToolNames.AppsList, "{}");
        Assert.True(appList.Succeeded);
        Assert.Contains("DTM AI", appList.ContentForModel, StringComparison.Ordinal);

        var appOpen = await executor.ExecuteAsync(
            AssistantToolNames.AppsOpen,
            $$"""{"app_id":"{{app!.Id}}"}""");
        Assert.True(appOpen.Succeeded);
        Assert.True(appOpen.ShouldLaunch);
        Assert.Equal(@"C:\Tools\DtmAi.exe", appOpen.LaunchTarget);

        var search = await executor.ExecuteAsync(AssistantToolNames.MusicSearch, """{"query":"YOASOBI"}""");
        Assert.True(search.Succeeded);
        Assert.Contains("demo catalog", search.ContentForModel, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Spotify API", search.ContentForModel, StringComparison.OrdinalIgnoreCase);

        var play = await executor.ExecuteAsync(AssistantToolNames.MusicPlay, """{"track_id":"demo-idol"}""");
        Assert.True(play.Succeeded);
        Assert.Contains("demo", play.ContentForModel, StringComparison.OrdinalIgnoreCase);

        var unknown = await executor.ExecuteAsync("powershell.invoke", "{}");
        Assert.False(unknown.Succeeded);
        Assert.Equal(AssistantUserMessages.ToolUnavailable, unknown.ErrorMessage);
    }

    [Fact]
    public async Task MusicPlay_OpensSpotify_WhenPlaybackCapabilityMissing()
    {
        var music = new MusicCommandService(new MusicService([OpenWebMusicProvider.Instance]));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            music: music);
        var search = await executor.ExecuteAsync(AssistantToolNames.MusicSearch, """{"query":"YOASOBI"}""");
        Assert.False(search.Succeeded);
        var play = await executor.ExecuteAsync(AssistantToolNames.MusicPlay, """{"query":"YOASOBI"}""");
        Assert.True(play.Succeeded);
        Assert.True(play.ShouldLaunch);
        Assert.True(play.LaunchIsExternalLink);
        Assert.StartsWith("https://open.spotify.com/", play.LaunchTarget, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spotify", play.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentTools_AddEvent_WritesLocalByDefault_AndRefuseUnregisteredFileDelete()
    {
        var day = new DateOnly(2026, 9, 3);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider();
        var calendar = new CalendarCommandService(
            new CalendarService([local]),
            new AgentToolTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));
        var workspace = new WorkspaceCommandService(calendar: calendar);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            calendar: calendar,
            workspace: workspace);

        var added = await executor.ExecuteAsync(
            AssistantToolNames.CalendarAddEvent,
            """{"title":"東進","hour":14,"minute":0}""");
        Assert.True(added.Succeeded);
        Assert.Contains("local calendar", added.ContentForModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("東進", added.ContentForModel, StringComparison.Ordinal);
        Assert.Single(local.ListAll());
        Assert.Equal("東進", local.ListAll()[0].Title);

        var deleted = await executor.ExecuteAsync(
            AssistantToolNames.FilesDelete,
            """{"name":"homework.pdf"}""");
        Assert.False(deleted.Succeeded);
        Assert.Contains("will not delete files on disk", deleted.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentTools_AddEvent_BatchWritesLocal_AndGoogleWhenRequested()
    {
        var day = new DateOnly(2026, 9, 3);
        var offset = TimeSpan.FromHours(9);
        var local = new LocalCalendarProvider();
        var google = new FakeGoogleCalendarWriter();
        var calendar = new CalendarCommandService(
            new CalendarService([local, google]),
            new AgentToolTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            calendar: calendar);

        var batch = await executor.ExecuteAsync(
            AssistantToolNames.CalendarAddEvent,
            """{"events":[{"title":"勉強","hour":16,"duration_minutes":120},{"title":"筋トレ","hour":18,"duration_minutes":60}]}""");
        Assert.True(batch.Succeeded);
        Assert.Equal(2, local.ListAll().Count);
        Assert.Contains("勉強", batch.ContentForModel, StringComparison.Ordinal);

        var googleAdd = await executor.ExecuteAsync(
            AssistantToolNames.CalendarAddEvent,
            """{"title":"東進","hour":14,"minute":0,"duration_minutes":60,"destination":"google"}""");
        Assert.True(googleAdd.Succeeded);
        Assert.Contains("Google Calendar", googleAdd.ContentForModel, StringComparison.OrdinalIgnoreCase);
        Assert.Single(google.Created);
        Assert.Equal("東進", google.Created[0].Title);
        Assert.Equal(14, google.Created[0].Start.Hour);
    }

    [Fact]
    public async Task CursorOpen_ReportsCursorCouldNotBeOpened_WhenUnavailable()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Pokemon",
            null,
            CreativeProjectType.Other,
            @"D:\src\pokemon",
            out var project,
            out _));
        var ai = new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => false);
        var executor = new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai);
        var result = await executor.ExecuteAsync(
            AssistantToolNames.CursorOpenProject,
            $$"""{"project_id":"{{project!.Id}}"}""");
        Assert.False(result.Succeeded);
        Assert.Equal(AssistantUserMessages.CursorOpenFailed, result.ErrorMessage);
    }
}

file sealed class AgentToolTime(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}

file sealed class FakeGoogleCalendarWriter : ICalendarProvider, ICalendarEventWriter
{
    public List<CalendarEvent> Created { get; } = [];

    public string ProviderId => CalendarProviderIds.Google + "-api";

    public string DisplayName => "Google Calendar";

    public string? OpenUrl => "https://calendar.google.com/";

    public CalendarProviderCapabilities Capabilities =>
        CalendarProviderCapabilities.ReadEvents
        | CalendarProviderCapabilities.CreateEvents
        | CalendarProviderCapabilities.Authentication;

    public CalendarAuthStatus AuthStatus => CalendarAuthStatus.Connected;

    public bool IsConfigured => true;

    public Task AuthenticateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<CalendarInfo>> GetCalendarsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarInfo>>([]);

    public Task<IReadOnlyList<CalendarEvent>> GetEventsAsync(
        CalendarQuery query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CalendarEvent>>(Created);

    public Task<CalendarEvent> CreateEventAsync(
        string title,
        DateTimeOffset start,
        DateTimeOffset end,
        bool isAllDay = false,
        CancellationToken cancellationToken = default)
    {
        var created = new CalendarEvent
        {
            Title = title,
            Start = start,
            End = end,
            IsAllDay = isAllDay,
            Provider = ProviderId,
            Source = "Google Calendar"
        };
        Created.Add(created);
        return Task.FromResult(created);
    }
}

public class AssistantServiceTests
{
    [Fact]
    public async Task SurfacesNotConfigured_WithoutPretendingSuccess()
    {
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new UnconfiguredAiProvider(AssistantProviderIds.OpenAi, "OpenAI"));

        // Needs the conversation model (not Local Fast Path agenda).
        var result = await service.SendAsync("今日の優先事項をAIに相談したい");
        Assert.False(result.Succeeded);
        Assert.True(result.NeedsConfiguration);
        Assert.True(result.ShowOpenSettingsAction);
        Assert.Contains(AssistantUserMessages.NotConfigured, result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(AssistantUserMessages.OpenSettings, result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SurfacesUnavailable_WhenProviderIsDown()
    {
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new UnavailableAiProvider(AssistantProviderIds.Gemini, "Gemini", AssistantUserMessages.Unavailable));

        var result = await service.SendAsync("hello");
        Assert.False(result.Succeeded);
        Assert.StartsWith(AssistantUserMessages.Unavailable, result.ErrorMessage);
        Assert.Contains("Provider:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Gemini", result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task SurfacesAuthenticationFailure_WithSettingsHint()
    {
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new UnavailableAiProvider(AssistantProviderIds.OpenAi, "OpenAI", AssistantUserMessages.AuthenticationFailed));

        var result = await service.SendAsync("hello");
        Assert.False(result.Succeeded);
        Assert.Equal(AssistantUserMessages.AuthenticationFailed, result.ErrorMessage);
        Assert.True(result.ShowOpenSettingsAction);
    }

    [Fact]
    public async Task RunsObservationTools_WithoutConfirmation()
    {
        var day = new DateOnly(2026, 8, 17);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "DTM",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
                    }
                ])
            ]),
            new AssistantFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" }
            ]),
            AiProviderResponse.Text("今日は1件予定があります。")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => provider);

        var result = await service.SendAsync("今日やることを教えて");
        Assert.True(result.Succeeded);
        Assert.Null(result.PendingConfirmation);
        Assert.Equal("今日は1件予定があります。", result.AssistantText);
        Assert.Contains(result.Activities, a => a.Text.Contains("calendar", StringComparison.OrdinalIgnoreCase)
                                               || a.Text.Contains("today", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AsksConfirmation_BeforeCursorOpen_ThenLaunchesAfterConfirm()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Pokemon",
            null,
            CreativeProjectType.Other,
            @"D:\src\pokemon",
            out var project,
            out _));
        var ai = new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => true);

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "c1",
                    Name = AssistantToolNames.CursorOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project!.Id}}"}"""
                }
            ]),
            AiProviderResponse.Text("Cursorで開きます。")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai),
            () => provider);

        var pending = await service.SendAsync("Pokemonプロジェクト開いて");
        Assert.True(pending.Succeeded);
        Assert.NotNull(pending.PendingConfirmation);
        Assert.Contains("Cursor", pending.PendingConfirmation!.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.False(pending.ShouldOpenCursorAtFolder);

        var confirmed = await service.ConfirmPendingAsync();
        Assert.True(confirmed.Succeeded);
        Assert.True(confirmed.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\src\pokemon", confirmed.CursorFolderPath);
        Assert.Equal("Cursorで開きます。", confirmed.AssistantText);
    }

    [Fact]
    public async Task CancelConfirmation_DoesNotLaunch()
    {
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "c1",
                    Name = AssistantToolNames.AppsOpen,
                    ArgumentsJson = """{"app_id":"nope"}"""
                }
            ]),
            AiProviderResponse.Text("Cancelled.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => provider);

        var pending = await service.SendAsync("open app");
        Assert.NotNull(pending.PendingConfirmation);
        service.CancelPending();
        var after = await service.ContinueAfterCancelAsync();
        Assert.True(after.Succeeded);
        Assert.False(after.ShouldLaunch);
        Assert.Equal("Cancelled.", after.AssistantText);
    }

    [Fact]
    public async Task RejectsUnknownTool_WithoutExecutingIt()
    {
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "x", Name = "shell.run", ArgumentsJson = """{"cmd":"rm -rf /"}""" }
            ]),
            AiProviderResponse.Text("That action is not available.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => provider);

        var result = await service.SendAsync("delete everything");
        Assert.True(result.Succeeded);
        Assert.Equal("That action is not available.", result.AssistantText);
        Assert.False(result.ShouldLaunch);
    }

    [Fact]
    public async Task CapsVisibleHistory()
    {
        var replies = Enumerable.Range(0, 30).Select(i => AiProviderResponse.Text($"r{i}"));
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new ScriptedAiProvider(replies));

        for (var i = 0; i < 30; i++)
        {
            await service.SendAsync($"m{i}");
        }

        Assert.True(service.VisibleHistory.Count <= AssistantService.MaxVisibleMessages);
        Assert.DoesNotContain(service.VisibleHistory, m => m.Content != null && m.Content.Contains("sk-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SongChange_WhenCatalogRefuses_OpensSpotifySearchWithoutAnotherModelCall()
    {
        var provider = new ToolThenTimeoutAiProvider(
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "m1",
                    Name = AssistantToolNames.MusicSearch,
                    ArgumentsJson = """{"query":"mr.children 深海"}"""
                }
            ]));
        var music = new MusicCommandService(new MusicService([new RefusingConnectedSearchProvider()]));
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, music: music),
            () => provider);

        var result = await service.SendAsync("曲をmr.childrenの深海にして");

        Assert.True(result.Succeeded);
        Assert.Equal(1, provider.Calls);
        Assert.Contains("Spotify", result.AssistantText, StringComparison.Ordinal);
        Assert.Contains("深海", result.AssistantText, StringComparison.Ordinal);
        Assert.True(result.ShouldLaunch);
        Assert.True(result.LaunchIsExternalLink);
        Assert.StartsWith("https://open.spotify.com/search/", result.LaunchTarget, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(AssistantUserMessages.Timeout, result.AssistantText ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmedSongChange_WhenCatalogRefuses_OpensSpotifySearch()
    {
        var provider = new ToolThenTimeoutAiProvider(
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "m1",
                    Name = AssistantToolNames.MusicPlay,
                    ArgumentsJson = """{"query":"innocent world"}"""
                }
            ]));
        var music = new MusicCommandService(new MusicService([new RefusingConnectedSearchProvider()]));
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, music: music),
            () => provider);

        var pending = await service.SendAsync("曲をinnocent worldにして");
        Assert.NotNull(pending.PendingConfirmation);
        Assert.False(pending.ShouldLaunch);

        var confirmed = await service.ConfirmPendingAsync();
        Assert.True(confirmed.Succeeded);
        Assert.Equal(1, provider.Calls);
        Assert.Contains("Spotify", confirmed.AssistantText, StringComparison.Ordinal);
        Assert.True(confirmed.ShouldLaunch);
        Assert.True(confirmed.LaunchIsExternalLink);
        Assert.StartsWith("https://open.spotify.com/search/", confirmed.LaunchTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FollowUpTimeout_AfterLocalTool_ReturnsTheToolNote()
    {
        var day = new DateOnly(2026, 8, 17);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "DTM",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(19, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(20, 0)), offset)
                    }
                ])
            ]),
            new AssistantFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(8, 0)), offset)));
        var provider = new ToolThenTimeoutAiProvider(
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "c1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" }
            ]));
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => provider);

        // Phrase that still requires the model tool loop (not Local Fast Path).
        var result = await service.SendAsync("calendar_get_today で予定を確認して要約して");
        Assert.True(result.Succeeded);
        Assert.Equal(2, provider.Calls);
        Assert.Contains("DTM", result.AssistantText, StringComparison.Ordinal);
        Assert.True(result.CanRetry);
    }

    [Fact]
    public async Task Timeout_WithoutTools_StaysAFailure()
    {
        var provider = new ToolThenTimeoutAiProvider(first: null);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => provider);

        var result = await service.SendAsync("hello");
        Assert.False(result.Succeeded);
        Assert.StartsWith(AssistantUserMessages.Timeout, result.ErrorMessage);
        Assert.Contains("Provider:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Gemini", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("AI Settings", result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(result.CanRetry);
    }
}

public class CalendarQueryUpcomingTests
{
    [Fact]
    public void ForUpcoming_ClampsDays()
    {
        var now = new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.FromHours(9));
        var query = CalendarQuery.ForUpcoming(now, 99);
        Assert.Equal(new DateOnly(2026, 8, 17), query.FromInclusive);
        Assert.Equal(new DateOnly(2026, 8, 31), query.ToInclusive);
        var min = CalendarQuery.ForUpcoming(now, 0);
        Assert.Equal(new DateOnly(2026, 8, 18), min.ToInclusive);
    }
}

file sealed class AssistantFixedTime(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}

file sealed class ToolThenTimeoutAiProvider(AiProviderResponse? first) : IAiProvider
{
    public int Calls { get; private set; }

    public string ProviderId => AssistantProviderIds.Gemini;

    public string DisplayName => "Gemini";

    public Task<AiProviderResponse> ChatAsync(
        IReadOnlyList<AiMessage> messages,
        IReadOnlyList<AssistantToolDefinition> tools,
        string model,
        CancellationToken cancellationToken = default)
    {
        _ = messages;
        _ = tools;
        _ = model;
        Calls++;
        cancellationToken.ThrowIfCancellationRequested();
        if (Calls == 1 && first is not null)
        {
            return Task.FromResult(first);
        }

        throw new TaskCanceledException();
    }
}

file sealed class RefusingConnectedSearchProvider : IMusicProvider
{
    public string ProviderId => "spotify";

    public string DisplayName => "Spotify";

    public MusicProviderCapabilities Capabilities =>
        MusicProviderCapabilities.Search | MusicProviderCapabilities.Authentication;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.Connected;

    public MusicTrack? CurrentTrack => null;

    public bool IsPlaying => false;

    public bool CanHandle(MusicSourceType type) => false;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = null;
        return false;
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Spotify's catalog API refused this account.");

    public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PreviousAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
