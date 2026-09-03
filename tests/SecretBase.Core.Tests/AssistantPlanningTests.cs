using SecretBase.Core.Ai;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Music;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Ai;

namespace SecretBase.Core.Tests;

public class AssistantIntentClassifierTests
{
    [Theory]
    [InlineData("Pokemonプロジェクトについて教えて", AssistantIntentKind.Question)]
    [InlineData("今日何をやればいい？", AssistantIntentKind.Suggestion)]
    [InlineData("Pokemonプロジェクトを開いて", AssistantIntentKind.ActionRequest)]
    [InlineData("open Pokemon in Cursor", AssistantIntentKind.ActionRequest)]
    [InlineData("tell me about Pokemon project", AssistantIntentKind.Question)]
    public void ClassifiesQuestionVsAction(string text, AssistantIntentKind expected)
    {
        Assert.Equal(expected, AssistantIntentClassifier.Classify(text));
    }
}

public class AssistantContextSelectorTests
{
    [Fact]
    public void SelectsMinimalScopes()
    {
        var agenda = AssistantContextSelector.FromUserText("今日の予定は？", AssistantIntentKind.Question);
        Assert.True(agenda.HasFlag(AssistantContextScope.Calendar));

        var todayPriority = AssistantContextSelector.FromUserText("今日何をやればいい？", AssistantIntentKind.Suggestion);
        Assert.True(todayPriority.HasFlag(AssistantContextScope.Calendar));
        Assert.True(todayPriority.HasFlag(AssistantContextScope.Creative));

        var musicOnly = AssistantContextSelector.FromUserText("音楽のおすすめ", AssistantIntentKind.Suggestion);
        Assert.True(musicOnly.HasFlag(AssistantContextScope.Music));

        var workMode = AssistantContextSelector.FromUserText("作業モードにして", AssistantIntentKind.ActionRequest);
        Assert.True(workMode.HasFlag(AssistantContextScope.Calendar));
        Assert.True(workMode.HasFlag(AssistantContextScope.Creative));
        Assert.True(workMode.HasFlag(AssistantContextScope.Music));
    }
}

public class AssistantPlannerTests
{
    [Fact]
    public void BuildsTodayPriorityAndStartProjectPlans_ClampedByMaxSteps()
    {
        var today = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.Suggestion,
            "今日何をやればいい？",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(today);
        Assert.Contains(today!.Steps, s => s.ToolName == AssistantToolNames.CalendarGetToday);
        Assert.Contains(today.Steps, s => s.Kind == AssistantPlanStepKind.Suggest);

        var start = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "Pokemonの開発を始めよう",
            snapshot: null,
            maxSteps: 2);
        Assert.NotNull(start);
        Assert.Equal(2, start!.Steps.Count);
        Assert.DoesNotContain(start.Steps, s => s.Index > 2);
    }

    [Fact]
    public void BuildsAgentPlans_ForMusicScheduleOpenAndDelete()
    {
        var play = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "あの曲をかけて",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(play);
        Assert.Contains(play!.Steps, s => s.ToolName == AssistantToolNames.MusicPlay && s.RequiresConfirmation);

        var usual = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "いつも通りの予定をいれて",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(usual);
        Assert.Contains(usual!.Steps, s => s.ToolName == AssistantToolNames.CalendarApplyUsual && s.RequiresConfirmation);

        var openFile = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "あのファイルを開いて",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(openFile);
        Assert.Contains(openFile!.Steps, s => s.ToolName == AssistantToolNames.WorkspaceOpenNamed);

        var delete = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "あのファイルを削除して",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(delete);
        Assert.Contains(delete!.Steps, s => s.ToolName == AssistantToolNames.FilesDelete && s.RequiresConfirmation);
    }

    [Fact]
    public void Settings_ClampsMaxSteps()
    {
        var settings = AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings
        {
            SchemaVersion = 1,
            MaxSteps = 99
        });
        Assert.Equal(AssistantSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.Equal(AssistantSettings.MaxStepsHardCap, settings.MaxSteps);
    }
}

public class AssistantPlanningWorkflowTests
{
    [Fact]
    public async Task Context_Plan_Confirmation_Action_Result_EndToEnd()
    {
        var day = new DateOnly(2026, 8, 24);
        var offset = TimeSpan.FromHours(9);
        var fixedTime = new PlanningFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset));
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "東進",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(16, 0)), offset)
                    }
                ])
            ]),
            fixedTime);

        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate(
            "Pokemon",
            "damage calc",
            CreativeProjectType.Programming,
            @"D:\src\pokemon",
            out var project,
            out _));
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects);
        var ai = new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => true);
        var context = new AssistantContextService(
            calendar: calendar,
            creative: creative,
            music: new MusicCommandService(new MusicService()),
            time: fixedTime,
            settings: () => new AssistantSettings { MaxSteps = 5 },
            isOpenAiKeyConfigured: () => true);

        var scoped = await context.GetSnapshotAsync(
            AssistantContextScope.Calendar | AssistantContextScope.Creative);
        Assert.True(scoped.Scope.HasFlag(AssistantContextScope.Calendar));
        Assert.NotEmpty(scoped.FreeTimeSlots);
        var formatted = AssistantContextService.FormatForModel(scoped);
        Assert.DoesNotContain(@"D:\src", formatted, StringComparison.OrdinalIgnoreCase);

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" },
                new AiToolCall { Id = "2", Name = AssistantToolNames.CreativeListProjects, ArgumentsJson = "{}" },
                new AiToolCall { Id = "3", Name = AssistantToolNames.ScheduleRecommend, ArgumentsJson = "{}" }
            ]),
            AiProviderResponse.Text(
                "予定と登録Projectを見ると、14時に東進があります。その前にPokemon Projectを進めるのが候補です。"),
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "c1",
                    Name = AssistantToolNames.CursorOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project!.Id}}"}"""
                }
            ]),
            AiProviderResponse.Text("Pokemon ProjectをCursorで開きました。")
        ]);

        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(
                BuiltinAssistantToolRegistry.Instance,
                calendar: calendar,
                creative: creative,
                ai: ai,
                context: context),
            () => provider,
            () => new AssistantSettings { MaxSteps = 5 },
            context);

        var advice = await service.SendAsync("今日何をやればいい？");
        Assert.True(advice.Succeeded);
        Assert.Equal(AssistantIntentKind.Suggestion, advice.Intent);
        Assert.NotNull(advice.Plan);
        Assert.Null(advice.PendingConfirmation);
        Assert.Contains("候補", advice.AssistantText, StringComparison.Ordinal);
        Assert.Contains(advice.Activities, a => a.Domain == AssistantActivityDomains.Calendar
                                               || a.Text.Contains("Calendar", StringComparison.OrdinalIgnoreCase));

        var start = await service.SendAsync("じゃあPokemonを始めよう");
        Assert.True(start.Succeeded);
        Assert.Equal(AssistantIntentKind.ActionRequest, start.Intent);
        Assert.NotNull(start.PendingConfirmation);
        Assert.NotEmpty(start.PendingConfirmation!.Actions);
        Assert.Equal(AssistantToolNames.CursorOpenProject, start.PendingConfirmation.Actions[0].ToolName);
        Assert.True(start.PendingConfirmation.HasRiskyAction);
        Assert.False(start.ShouldOpenCursorAtFolder);

        var run = await service.ConfirmPendingAsync();
        Assert.True(run.Succeeded);
        Assert.True(run.ShouldOpenCursorAtFolder);
        Assert.Equal(@"D:\src\pokemon", run.CursorFolderPath);
        Assert.Contains(run.ActionResults, r => r.Succeeded && r.ToolName == AssistantToolNames.CursorOpenProject);
        Assert.Contains("開きました", run.AssistantText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestTools_DoNotRequireConfirmation_HostActionRejected()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out _, out _));
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects);
        var context = new AssistantContextService(creative: creative);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            creative: creative,
            music: new MusicCommandService(new MusicService()),
            context: context);

        var projectRec = await executor.ExecuteAsync(AssistantToolNames.ProjectRecommend, "{}");
        Assert.True(projectRec.Succeeded);
        Assert.Contains("Candidate", projectRec.ContentForModel, StringComparison.OrdinalIgnoreCase);

        var schedule = await executor.ExecuteAsync(AssistantToolNames.ScheduleRecommend, "{}");
        Assert.True(schedule.Succeeded);
        Assert.Contains("candidates", schedule.ContentForModel, StringComparison.OrdinalIgnoreCase);

        var music = await executor.ExecuteAsync(AssistantToolNames.MusicRecommend, """{"query":"focus"}""");
        Assert.True(music.Succeeded);
        Assert.Contains("demo catalog", music.ContentForModel, StringComparison.OrdinalIgnoreCase);

        var host = new AssistantToolDefinition
        {
            Name = "shell.run",
            Description = "blocked",
            Capability = AssistantToolCapability.HostAction
        };
        Assert.True(AssistantConfirmationPolicy.IsHostActionOnly(host));
        Assert.False(AssistantConfirmationPolicy.CanAutoExecute(host));
    }

    [Fact]
    public async Task MultiActionConfirmation_AndCancel()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
        var creative = new CreativeCommandService(
            new CreativeWorkspaceService(new MemoryCreativeWorkspaceStore()),
            projects);
        var ai = new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => true);

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "a",
                    Name = AssistantToolNames.CreativeOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project!.Id}}"}"""
                },
                new AiToolCall
                {
                    Id = "b",
                    Name = AssistantToolNames.CursorOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project.Id}}"}"""
                }
            ]),
            AiProviderResponse.Text("Cancelled path.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, creative: creative, ai: ai),
            () => provider);

        var pending = await service.SendAsync("Pokemonを開いてCursorも起動して");
        Assert.NotNull(pending.PendingConfirmation);
        Assert.Equal(2, pending.PendingConfirmation!.Actions.Count);
        Assert.True(pending.PendingConfirmation.HasRiskyAction);

        service.CancelPending();
        var after = await service.ContinueAfterCancelAsync();
        Assert.True(after.Succeeded);
        Assert.False(after.ShouldOpenCursorAtFolder);
        Assert.False(after.ShouldLaunch);
    }

    [Fact]
    public async Task RetryableProviderErrors_PreserveUserTextForRetry()
    {
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new UnavailableAiProvider(AssistantProviderIds.OpenAi, "OpenAI", AssistantUserMessages.NetworkError));

        var result = await service.SendAsync("今日の予定を教えて");
        Assert.False(result.Succeeded);
        Assert.True(result.CanRetry);
        Assert.Equal("今日の予定を教えて", result.RetryUserText);
    }

    [Fact]
    public async Task SessionHistory_ExcludesSecrets_AndRespectsLimit()
    {
        var replies = Enumerable.Range(0, 25).Select(i => AiProviderResponse.Text($"ok{i}"));
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => new ScriptedAiProvider(replies));

        var rejected = await service.SendAsync("here is sk-secretkey1234567890");
        Assert.False(rejected.Succeeded);

        for (var i = 0; i < 25; i++)
        {
            await service.SendAsync($"msg{i}");
        }

        Assert.True(service.VisibleHistory.Count <= AssistantService.MaxVisibleMessages);
        Assert.DoesNotContain(service.VisibleHistory, m => (m.Content ?? string.Empty).Contains("sk-", StringComparison.Ordinal));
    }

    [Fact]
    public void ComputeFreeTime_UsesOffsetCalendarDate_NotMachineLocalTimezone()
    {
        // 2026-08-24 09:00 +09:00 — calendar date must stay Aug 24 even when machine TZ differs.
        var now = new DateTimeOffset(2026, 8, 24, 9, 0, 0, TimeSpan.FromHours(9));
        var events = new[]
        {
            new CalendarEvent
            {
                Title = "東進",
                Provider = CalendarProviderIds.Local,
                Start = new DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.FromHours(9)),
                End = new DateTimeOffset(2026, 8, 24, 16, 0, 0, TimeSpan.FromHours(9))
            }
        };

        var free = AssistantContextService.ComputeFreeTime(events, now);

        Assert.NotEmpty(free);
        Assert.All(free, slot => Assert.Equal(TimeSpan.FromHours(9), slot.Start.Offset));
    }

    [Fact]
    public async Task FreeTime_ComputedFromBusyBlocks()
    {
        var now = new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.FromHours(9));
        var events = new[]
        {
            new CalendarEvent
            {
                Title = "Meeting",
                Provider = CalendarProviderIds.Local,
                Start = new DateTimeOffset(2026, 8, 24, 14, 0, 0, TimeSpan.FromHours(9)),
                End = new DateTimeOffset(2026, 8, 24, 15, 0, 0, TimeSpan.FromHours(9))
            }
        };
        var free = AssistantContextService.ComputeFreeTime(events, now);
        Assert.NotEmpty(free);
        Assert.True(free.All(s => s.End > s.Start));
        Assert.Contains(free, s => s.End <= events[0].Start || s.Start >= events[0].End);
    }
}

file sealed class PlanningFixedTime(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}
