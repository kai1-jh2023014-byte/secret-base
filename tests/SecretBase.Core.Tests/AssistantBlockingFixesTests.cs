using SecretBase.Core.Ai;
using SecretBase.Core.Assistant;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Widgets.Ai;

namespace SecretBase.Core.Tests;

public class AssistantConfirmationStateTests
{
    [Fact]
    public async Task SendWhilePendingConfirmation_IsRejected_AndPendingPreserved()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
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
            AiProviderResponse.Text("Should not run yet.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai),
            () => provider);

        var pending = await service.SendAsync("PokemonをCursorで開いて");
        Assert.True(pending.Succeeded);
        Assert.NotNull(pending.PendingConfirmation);
        Assert.True(service.HasPendingConfirmation);

        var blocked = await service.SendAsync("別のメッセージ");
        Assert.False(blocked.Succeeded);
        Assert.Equal(AssistantUserMessages.PendingConfirmationMustResolve, blocked.ErrorMessage);
        Assert.True(service.HasPendingConfirmation);
        Assert.NotNull(pending.PendingConfirmation);
    }

    [Fact]
    public async Task CancelPending_ClearsPendingState()
    {
        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall
                {
                    Id = "a1",
                    Name = AssistantToolNames.AppsOpen,
                    ArgumentsJson = """{"app_id":"demo"}"""
                }
            ]),
            AiProviderResponse.Text("Cancelled.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => provider);

        var pending = await service.SendAsync("open app");
        Assert.True(service.HasPendingConfirmation);

        service.CancelPending();
        Assert.False(service.HasPendingConfirmation);

        var after = await service.ContinueAfterCancelAsync();
        Assert.True(after.Succeeded);
        Assert.False(after.ShouldLaunch);
    }

    [Fact]
    public async Task ConfirmPendingAsync_ClearsPendingState_AndExecutes()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
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
            AiProviderResponse.Text("Opened.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai),
            () => provider);

        await service.SendAsync("open Pokemon in Cursor");
        Assert.True(service.HasPendingConfirmation);

        var run = await service.ConfirmPendingAsync();
        Assert.True(run.Succeeded);
        Assert.False(service.HasPendingConfirmation);
        Assert.True(run.ShouldOpenCursorAtFolder);
    }

    [Fact]
    public async Task DuplicateConfirmPendingAsync_FailsWhenNothingPending()
    {
        var provider = new ScriptedAiProvider([AiProviderResponse.Text("ok")]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance),
            () => provider);

        var second = await service.ConfirmPendingAsync();
        Assert.False(second.Succeeded);
        Assert.Equal("Nothing to confirm.", second.ErrorMessage);
    }

    [Fact]
    public async Task AfterSuccessfulConfirm_NewSend_IsAllowed()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
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
            AiProviderResponse.Text("Opened."),
            AiProviderResponse.Text("Follow-up ok.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai),
            () => provider);

        await service.SendAsync("open Pokemon");
        await service.ConfirmPendingAsync();
        Assert.False(service.HasPendingConfirmation);

        var followUp = await service.SendAsync("thanks");
        Assert.True(followUp.Succeeded);
        Assert.Equal("Follow-up ok.", followUp.AssistantText);
    }
}

public class AssistantStepBudgetTests
{
    [Fact]
    public async Task ReadsThenConfirm_RemainsExecutableWithinBudget()
    {
        var day = new DateOnly(2026, 8, 24);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "Study",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(15, 0)), offset)
                    }
                ])
            ]),
            new BlockingFixesFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset)));

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
                new AiToolCall { Id = "1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" },
                new AiToolCall { Id = "2", Name = AssistantToolNames.CreativeListProjects, ArgumentsJson = "{}" },
                new AiToolCall { Id = "3", Name = AssistantToolNames.ScheduleRecommend, ArgumentsJson = "{}" },
                new AiToolCall { Id = "4", Name = AssistantToolNames.ProjectRecommend, ArgumentsJson = "{}" },
                new AiToolCall
                {
                    Id = "c1",
                    Name = AssistantToolNames.CursorOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project!.Id}}"}"""
                }
            ]),
            AiProviderResponse.Text("Done.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(
                BuiltinAssistantToolRegistry.Instance,
                calendar: calendar,
                creative: creative,
                ai: ai),
            () => provider,
            () => new AssistantSettings { MaxSteps = 5 });

        var pending = await service.SendAsync("Pokemonを始めよう");
        Assert.NotNull(pending.PendingConfirmation);
        Assert.Equal(AssistantToolNames.CursorOpenProject, pending.PendingConfirmation!.Actions[0].ToolName);

        var run = await service.ConfirmPendingAsync();
        Assert.True(run.Succeeded);
        Assert.True(run.ShouldOpenCursorAtFolder);
        Assert.Contains(run.ActionResults, r => r.Succeeded && r.ToolName == AssistantToolNames.CursorOpenProject);
    }

    [Fact]
    public async Task MultipleConfirmedActions_FitWithinBudget()
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
            AiProviderResponse.Text("Both ran.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, creative: creative, ai: ai),
            () => provider,
            () => new AssistantSettings { MaxSteps = 3 });

        var pending = await service.SendAsync("open dashboard and Cursor");
        Assert.NotNull(pending.PendingConfirmation);
        Assert.Equal(2, pending.PendingConfirmation!.Actions.Count);

        var run = await service.ConfirmPendingAsync();
        Assert.True(run.Succeeded);
        Assert.Equal(2, run.ActionResults.Count);
        Assert.All(run.ActionResults, r => Assert.True(r.Succeeded));
        Assert.True(run.ShouldOpenCursorAtFolder);
    }

    [Fact]
    public async Task OverBudgetConfirm_IsRejectedSafely_WithoutConfirmPrompt()
    {
        var day = new DateOnly(2026, 8, 24);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "Busy",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(11, 0)), offset)
                    }
                ])
            ]),
            new BlockingFixesFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset)));

        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
        var ai = new AiCommandService(AiWorkspaceWidgetConfiguration.CreateDefault(), projects, () => true);

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" },
                new AiToolCall { Id = "2", Name = AssistantToolNames.CalendarGetUpcoming, ArgumentsJson = "{}" },
                new AiToolCall
                {
                    Id = "c1",
                    Name = AssistantToolNames.CursorOpenProject,
                    ArgumentsJson = $$"""{"project_id":"{{project!.Id}}"}"""
                }
            ]),
            AiProviderResponse.Text("No launch.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(
                BuiltinAssistantToolRegistry.Instance,
                calendar: calendar,
                ai: ai),
            () => provider,
            () => new AssistantSettings { MaxSteps = 2 });

        var result = await service.SendAsync("open Pokemon after reads");
        Assert.Null(result.PendingConfirmation);
        Assert.False(result.ShouldOpenCursorAtFolder);
        Assert.False(service.HasPendingConfirmation);
    }

    [Fact]
    public async Task MaxStepsStillCapsAutoTools_InSameBatch()
    {
        var day = new DateOnly(2026, 8, 24);
        var offset = TimeSpan.FromHours(9);
        var calendar = new CalendarCommandService(
            new CalendarService(
            [
                new LocalCalendarProvider(
                [
                    new CalendarEvent
                    {
                        Title = "A",
                        Provider = CalendarProviderIds.Local,
                        Start = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), offset),
                        End = new DateTimeOffset(day.ToDateTime(new TimeOnly(11, 0)), offset)
                    }
                ])
            ]),
            new BlockingFixesFixedTime(new DateTimeOffset(day.ToDateTime(new TimeOnly(9, 0)), offset)));

        var provider = new ScriptedAiProvider(
        [
            AiProviderResponse.Tools(
            [
                new AiToolCall { Id = "1", Name = AssistantToolNames.CalendarGetToday, ArgumentsJson = "{}" },
                new AiToolCall { Id = "2", Name = AssistantToolNames.CalendarGetUpcoming, ArgumentsJson = "{}" }
            ]),
            AiProviderResponse.Text("One step only.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, calendar: calendar),
            () => provider,
            () => new AssistantSettings { MaxSteps = 1 });

        var result = await service.SendAsync("today and upcoming");
        Assert.False(result.Succeeded);
        Assert.Equal(AssistantUserMessages.MaxStepsReached, result.ErrorMessage);
        Assert.Null(result.PendingConfirmation);
        Assert.True(result.CanRetry);
        Assert.Equal(
            1,
            result.Activities.Count(a => a.Status == AssistantActivityStatus.Done));
    }
}

public class AssistantConfirmationToggleTests
{
    [Fact]
    public async Task LaunchTools_StillRequireConfirmation_WhenSettingDisabledInStore()
    {
        var projects = new CreativeProjectService(new MemoryCreativeProjectStore());
        Assert.True(projects.TryCreate("Pokemon", null, CreativeProjectType.Other, @"D:\src\pokemon", out var project, out _));
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
            AiProviderResponse.Text("Opened.")
        ]);
        var service = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, ai: ai),
            () => provider,
            () => new AssistantSettings { MaxSteps = 5, RequireConfirmationForActions = false });

        var pending = await service.SendAsync("open Pokemon in Cursor");
        Assert.NotNull(pending.PendingConfirmation);
        Assert.False(pending.ShouldOpenCursorAtFolder);

        var run = await service.ConfirmPendingAsync();
        Assert.True(run.ShouldOpenCursorAtFolder);
    }

    [Fact]
    public void Migrator_AlwaysForcesConfirmationRequired()
    {
        var migrated = AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings
        {
            SchemaVersion = 2,
            RequireConfirmationForActions = false
        });
        Assert.True(migrated.RequireConfirmationForActions);
    }
}

file sealed class BlockingFixesFixedTime(DateTimeOffset instant) : SecretBase.Core.Time.ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}
