using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Base;
using SecretBase.Core.Creative;
using SecretBase.Core.Focus;
using SecretBase.Core.Music;
using SecretBase.Core.Todo;
using SecretBase.Core.Widgets;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class SpotifyOpenUrlTests
{
    [Fact]
    public void TryCreateTrackUrl_AcceptsSpotifyId()
    {
        Assert.True(SpotifyWebSearch.TryCreateTrackUrl("4uLU6hMCjMI75M1A2tKUQC", out var url, out _));
        Assert.Equal("https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC", url);
    }

    [Fact]
    public void TryCreateOpenUrl_PrefersTrackPage_ForSpotifyTrack()
    {
        var track = new MusicTrack
        {
            Id = "4uLU6hMCjMI75M1A2tKUQC",
            Title = "Never Gonna Give You Up",
            Artist = "Rick Astley",
            ProviderId = "spotify"
        };
        Assert.True(SpotifyWebSearch.TryCreateOpenUrl(track, null, out var url, out _));
        Assert.StartsWith("https://open.spotify.com/track/", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateOpenUrl_FallsBackToSearch_ForDemoTrack()
    {
        var track = new MusicTrack
        {
            Id = "demo-idol",
            Title = "Idol",
            Artist = "YOASOBI",
            ProviderId = DemoCatalogMusicProvider.Id
        };
        Assert.True(SpotifyWebSearch.TryCreateOpenUrl(track, null, out var url, out _));
        Assert.StartsWith("https://open.spotify.com/search/", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("YOASOBI", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateDesktopUri_BuildsSpotifySearchUri()
    {
        Assert.True(SpotifyWebSearch.TryCreateDesktopUri(null, "Pretender", out var uri, out _));
        Assert.StartsWith("spotify:search:", uri, StringComparison.Ordinal);
    }
}

public class MusicPremiumFallbackTests
{
    [Fact]
    public async Task Play_WhenPremiumRequired_AttachesSpotifyOpenUrl()
    {
        var provider = new PremiumRequiredPlaybackProvider();
        var music = new MusicCommandService(new MusicService([provider]));
        music.RememberTracks(
        [
            new MusicTrack
            {
                Id = "4uLU6hMCjMI75M1A2tKUQC",
                Title = "Pretender",
                Artist = "Official髭男dism",
                ProviderId = "spotify"
            }
        ]);

        var result = await music.ExecuteAsync(
            MusicCommand.PlayTrackById("4uLU6hMCjMI75M1A2tKUQC", "spotify", query: "Pretender"));

        Assert.False(result.Succeeded);
        Assert.Contains("Premium", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://open.spotify.com/track/", result.WebSearchUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MusicPlayTool_WhenPlaybackMissing_OpensSpotifyInsteadOfFailing()
    {
        var music = new MusicCommandService(new MusicService([OpenWebMusicProvider.Instance]));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            music: music);

        var play = await executor.ExecuteAsync(
            AssistantToolNames.MusicPlay,
            """{"query":"YOASOBIの音楽"}""");

        Assert.True(play.Succeeded);
        Assert.True(play.ShouldLaunch);
        Assert.True(play.LaunchIsExternalLink);
        Assert.StartsWith("https://open.spotify.com/", play.LaunchTarget, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spotify", play.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MusicPlayTool_WhenPremiumPlayFails_OpensTrackPage()
    {
        var provider = new PremiumRequiredPlaybackProvider();
        var music = new MusicCommandService(new MusicService([provider]));
        music.RememberTracks(
        [
            new MusicTrack
            {
                Id = "4uLU6hMCjMI75M1A2tKUQC",
                Title = "Pretender",
                Artist = "Official髭男dism",
                ProviderId = "spotify"
            }
        ]);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            music: music);

        var play = await executor.ExecuteAsync(
            AssistantToolNames.MusicPlay,
            """{"track_id":"4uLU6hMCjMI75M1A2tKUQC","query":"Pretender"}""");

        Assert.True(play.Succeeded);
        Assert.True(play.ShouldLaunch);
        Assert.StartsWith("https://open.spotify.com/track/", play.LaunchTarget, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_MapsOpenMusicPhrases()
    {
        foreach (var phrase in new[]
                 {
                     "YOASOBIの音楽を開いて",
                     "あの曲をかけて",
                     "play this song",
                     "Official髭男dismのアルバムを再生して"
                 })
        {
            var plan = AssistantPlanner.TryBuildFromIntent(
                AssistantIntentKind.ActionRequest,
                phrase,
                snapshot: null,
                maxSteps: 5);
            Assert.NotNull(plan);
            Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.MusicPlay && s.RequiresConfirmation);
        }
    }
}

public class CodingEnvironmentCapabilityTests
{
    [Fact]
    public void Planner_MapsCodingEnvironmentPhrases()
    {
        foreach (var phrase in new[]
                 {
                     "プログラミング環境を開いて",
                     "開発環境をセットアップして",
                     "open coding environment",
                     "programming environment please"
                 })
        {
            var plan = AssistantPlanner.TryBuildFromIntent(
                AssistantIntentKind.ActionRequest,
                phrase,
                snapshot: null,
                maxSteps: 5);
            Assert.NotNull(plan);
            Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.CodingEnvironmentSetup);
            Assert.Contains(plan.Steps, s => s.ToolName == AssistantToolNames.CreativeListProjects);
            Assert.Contains(plan.Steps, s => s.ToolName == AssistantToolNames.TodoList);
        }
    }

    [Fact]
    public async Task CodingEnvironmentSetup_StartsPomodoro_EnsuresWidgets_AndArranges()
    {
        var focus = new FocusSessionStore();
        var todos = new MemoryTodoStore();
        todos.Save(new TodoList { Items = [TodoItem.Create("Ship Base AI")] });
        var project = new CreativeProject
        {
            Id = "p1",
            Name = "Secret Base",
            IsFavorite = true,
            ProjectType = CreativeProjectType.Programming
        };
        var baseServices = new BaseExperienceServices(
            todos,
            focus,
            () => [project],
            () => [new CustomApp { Name = "Cursor", CreativeProjectId = "p1" }],
            () => [],
            () => new DateTimeOffset(2026, 10, 7, 14, 0, 0, TimeSpan.Zero));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: baseServices);

        var result = await executor.ExecuteAsync(
            AssistantToolNames.CodingEnvironmentSetup,
            """{"intent":"Secret Base development","minutes":25}""");

        Assert.True(result.Succeeded);
        Assert.False(result.ShouldLaunch);
        Assert.True(result.ShouldArrangeDesktop);
        Assert.True(focus.Current.IsRunning);
        Assert.NotNull(baseServices.CurrentWorkspace);
        Assert.Contains(WidgetTypes.Creative, result.EnsureWidgetType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WidgetTypes.Pomodoro, result.EnsureWidgetType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WidgetTypes.Workspace, result.EnsureWidgetType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Coding environment", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Todo", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Registry_MarksCodingEnvironment_AsSafeAuto()
    {
        var tool = BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.CodingEnvironmentSetup);
        Assert.NotNull(tool);
        Assert.Equal(AssistantToolCapability.SafeAuto, tool!.Capability);
        Assert.False(tool.RequiresConfirmation);
    }

    [Fact]
    public void MergeEnsureWidgetTypes_DedupesCommaLists()
    {
        var merged = AssistantService.MergeEnsureWidgetTypes(
            WidgetTypes.Pomodoro,
            $"{WidgetTypes.Creative},{WidgetTypes.Pomodoro},{WidgetTypes.Workspace}");
        Assert.Equal(
            $"{WidgetTypes.Pomodoro},{WidgetTypes.Creative},{WidgetTypes.Workspace}",
            merged);
    }
}

public class OpenAppByNameCapabilityTests
{
    [Fact]
    public void Planner_MapsOpenAppPhrases()
    {
        foreach (var phrase in new[]
                 {
                     "Cursorのアプリを開いて",
                     "DTM AIアプリを起動して",
                     "open Notion app",
                     "launch the Chrome application"
                 })
        {
            var plan = AssistantPlanner.TryBuildFromIntent(
                AssistantIntentKind.ActionRequest,
                phrase,
                snapshot: null,
                maxSteps: 5);
            Assert.NotNull(plan);
            Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.AppsOpen && s.RequiresConfirmation);
            Assert.Contains(plan.Steps, s => s.ToolName == AssistantToolNames.AppsList);
        }
    }

    [Fact]
    public async Task AppsOpen_ResolvesByName_FromAllowlist()
    {
        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        Assert.True(apps.Apps.TryAdd(new CustomApp
        {
            Name = "Cursor",
            Type = CustomAppType.Application,
            LaunchTarget = @"C:\Tools\Cursor\Cursor.exe"
        }, out _, out _));
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            apps: apps);

        var result = await executor.ExecuteAsync(
            AssistantToolNames.AppsOpen,
            """{"name":"Cursorのアプリ"}""");

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldLaunch);
        Assert.Equal(@"C:\Tools\Cursor\Cursor.exe", result.LaunchTarget);
        Assert.Contains("Cursor", result.ContentForModel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AppsOpen_FallsBackToWorkspaceNamed_WhenNotInMyApps()
    {
        var apps = new AppCommandService(new CustomAppService(new MemoryCustomAppStore()));
        var catalog = new BlockOnlyCatalog();
        var workspace = new WorkspaceCommandService(apps: apps, catalog: catalog);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            apps: apps,
            workspace: workspace);

        var result = await executor.ExecuteAsync(
            AssistantToolNames.AppsOpen,
            """{"name":"VS Code"}""");

        Assert.True(result.Succeeded);
        Assert.True(result.ShouldLaunch);
        Assert.Equal(@"C:\Tools\Code.exe", result.LaunchTarget);
    }

    [Fact]
    public void StripAppPhrase_RemovesJapaneseSuffix()
    {
        Assert.Equal("Cursor", AssistantToolExecutor.StripAppPhrase("Cursorのアプリ"));
        Assert.Equal("Notion", AssistantToolExecutor.StripAppPhrase("Notion app"));
    }

    private sealed class BlockOnlyCatalog : IWorkspaceCatalog
    {
        public IReadOnlyList<WorkspaceNamedEntry> ListBlockItems() =>
        [
            new()
            {
                Name = "VS Code",
                Kind = WorkspaceEntryKind.BlockItem,
                BlockId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                ItemId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                LaunchTarget = @"C:\Tools\Code.exe"
            }
        ];

        public bool TryRemoveBlockItem(Guid blockId, Guid itemId, out string? error)
        {
            error = "not used";
            return false;
        }
    }
}

file sealed class PremiumRequiredPlaybackProvider : IMusicProvider
{
    public string ProviderId => "spotify";

    public string DisplayName => "Spotify";

    public MusicProviderCapabilities Capabilities =>
        MusicProviderCapabilities.Search
        | MusicProviderCapabilities.Playback
        | MusicProviderCapabilities.Authentication;

    public MusicAuthStatus AuthStatus => MusicAuthStatus.Connected;

    public MusicTrack? CurrentTrack => null;

    public bool IsPlaying => false;

    public bool CanHandle(MusicSourceType type) => type == MusicSourceType.Spotify;

    public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
    {
        url = null;
        error = null;
        return false;
    }

    public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MusicTrack>>(Array.Empty<MusicTrack>());

    public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Spotify Premium and an active device are required for playback control.");

    public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task PreviousAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
