using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Context;
using SecretBase.Core.Creative;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Security;
using SecretBase.Core.Time;
using SecretBase.Core.Todo;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class BaseContextAggregatorTests
{
    [Fact]
    public void CachesUntilInvalidated()
    {
        var calls = 0;
        var provider = new StaticContextProvider("time", () =>
        {
            calls++;
            return new BaseContextSlice("time", "now", ["tick"]);
        });
        var aggregator = new BaseContextAggregator([provider], TimeSpan.FromMinutes(1));
        var t0 = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        aggregator.GetSnapshot(t0);
        aggregator.GetSnapshot(t0.AddSeconds(5));
        Assert.Equal(1, calls);
        aggregator.Invalidate();
        aggregator.GetSnapshot(t0.AddSeconds(6));
        Assert.Equal(2, calls);
    }
}

public class WorkspacePreparerTests
{
    [Fact]
    public void MatchesFavoriteProject_AndDoesNotInventPaths()
    {
        var project = new CreativeProject
        {
            Id = "p1",
            Name = "Secret Base",
            IsFavorite = true,
            Resources =
            [
                new CreativeProjectResource { Name = "AGENTS.md", Kind = CreativeProjectResourceKind.File }
            ]
        };
        var session = WorkspacePreparer.Prepare(
            "Secret Baseの開発を続けたい",
            [project],
            [new CustomApp { Name = "Cursor", CreativeProjectId = "p1" }],
            new TodoList { Items = [TodoItem.Create("Finish Base AI")] },
            [],
            DateTimeOffset.UtcNow);

        Assert.Equal("Secret Base", session.ProjectName);
        Assert.Contains("AGENTS.md", session.SuggestedFileNames);
        Assert.Contains("Cursor", session.SuggestedAppNames);
        Assert.Contains(session.PreparedChecks, check => check.Contains("Git", StringComparison.Ordinal));
        var card = WorkspacePreparer.FormatCard(session);
        Assert.DoesNotContain("C:\\", card, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Prepared:", card, StringComparison.Ordinal);
    }
}

public class FileIntelligenceTests
{
    [Fact]
    public void SuggestsUnusedRegisteredFiles_WithoutPaths()
    {
        var project = new CreativeProject
        {
            Name = "Secret Base",
            Resources =
            [
                new CreativeProjectResource { Name = "old.txt", Kind = CreativeProjectResourceKind.File, Target = @"C:\secret\old.txt" }
            ]
        };
        var candidates = FileIntelligence.SuggestCleanup([project], DateTimeOffset.UtcNow);
        Assert.Contains(candidates, c => c.Name == "old.txt");
        var text = FileIntelligence.FormatSuggestion(candidates);
        Assert.DoesNotContain(@"C:\secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("will not delete", text, StringComparison.OrdinalIgnoreCase);
    }
}

public class TimeAwareAdvisorTests
{
    [Fact]
    public void SuggestsPrepare_WhenWorkBlockStartsSoon_AndNeverRequiresLaunch()
    {
        var now = new DateTimeOffset(2026, 9, 3, 13, 50, 0, TimeSpan.Zero);
        var suggestion = TimeAwareAdvisor.Suggest(
            now,
            [
                new CalendarEvent
                {
                    Title = "開発時間",
                    Start = now.AddMinutes(8),
                    End = now.AddHours(2)
                }
            ]);
        Assert.NotNull(suggestion);
        Assert.Equal("prepare", suggestion!.Kind);
        Assert.False(suggestion.RequiresConfirmation);
    }

    [Fact]
    public void StaysQuiet_WhenFocusIsRunning()
    {
        var now = DateTimeOffset.UtcNow;
        var focus = new FocusSessionStore().Start(now);
        var suggestion = TimeAwareAdvisor.Suggest(
            now,
            [
                new CalendarEvent { Title = "開発", Start = now.AddMinutes(5), End = now.AddHours(1) }
            ],
            focus);
        Assert.Null(suggestion);
    }
}

public class AutomationSafetyTests
{
    [Fact]
    public void MapsExistingTools_WithoutWeakeningConfirmation()
    {
        var registry = BuiltinAssistantToolRegistry.Instance;
        Assert.Equal(AutomationSafetyLevel.SafeAuto, AutomationSafety.ForTool(registry.Find(AssistantToolNames.WorkspacePrepare)!));
        Assert.Equal(AutomationSafetyLevel.ConfirmationRequired, AutomationSafety.ForTool(registry.Find(AssistantToolNames.AppsOpen)!));
        Assert.Equal(AutomationSafetyLevel.ExplicitConfirmationRequired, AutomationSafety.ForTool(registry.Find(AssistantToolNames.FilesDelete)!));
        Assert.False(AutomationSafety.CanRunWithoutPrompt(registry.Find(AssistantToolNames.WorkspaceContinue)!));
        Assert.True(AutomationSafety.CanRunWithoutPrompt(registry.Find(AssistantToolNames.FocusStart)!));
    }
}

public class BaseAiStatusFormatterTests
{
    [Fact]
    public void RemoteWithoutKey_ShowsLocal()
    {
        Assert.Equal("Base AI ● Local", BaseAiStatusFormatter.Format(new AssistantProviderStatusInfo
        {
            ProviderId = AssistantProviderIds.OpenAi,
            IsConfigured = false
        }));
        Assert.Equal("Base AI ● Online", BaseAiStatusFormatter.Format(new AssistantProviderStatusInfo
        {
            ProviderId = AssistantProviderIds.OpenAi,
            IsConfigured = true
        }));
        Assert.Equal("Base AI ● Local", BaseAiStatusFormatter.Format(new AssistantProviderStatusInfo
        {
            ProviderId = AssistantProviderIds.Local,
            IsConfigured = true
        }));
    }
}

public class BaseOnboardingMigrationTests
{
    [Fact]
    public void ExistingLayout_MarksOnboardingComplete()
    {
        var migrated = BaseSettingsMigrator.MigrateToCurrent(null, layoutAlreadyExisted: true);
        Assert.True(migrated.OnboardingCompleted);
        Assert.Equal("Atelier", BaseAtmosphere.ToThemePreset(BaseAtmosphere.Calm));
    }
}

public class ClockBaseStatusTests
{
    [Fact]
    public void ShowsNextEvent_AndProviderLine()
    {
        var now = new DateTimeOffset(2026, 9, 3, 23, 42, 0, TimeSpan.Zero);
        var status = ClockBaseStatusComposer.Compose(
            now,
            [new CalendarEvent { Title = "Development", Start = now.AddMinutes(18), End = now.AddHours(2) }],
            workspace: null,
            focus: null,
            provider: new AssistantProviderStatusInfo { ProviderId = AssistantProviderIds.Local, IsConfigured = true });
        Assert.Contains("Development", status.NextLine, StringComparison.Ordinal);
        Assert.Equal("Base AI ● Local", status.StatusLine);
    }
}

public class ClockBaseStyleTests
{
    [Fact]
    public void DefaultClock_UsesBaseStyle()
    {
        var clock = DefaultWidgetFactory.CreateDefaultClock();
        var config = ClockWidgetConfiguration.FromDictionary(clock.Configuration);
        Assert.Equal(ClockWidgetConfiguration.StyleBase, config.DisplayStyle);
        Assert.False(config.ShowSeconds);
        Assert.Contains(WidgetCatalog.Entries, e => e.WidgetType == WidgetTypes.Workspace);
    }
}

public class BaseExperienceToolTests
{
    [Fact]
    public async Task PrepareIsSafeAuto_ContinueRequiresProject_DeleteStillRefusesDisk()
    {
        var todos = new MemoryTodoStore();
        var focus = new FocusSessionStore();
        var project = new CreativeProject
        {
            Id = "p1",
            Name = "Secret Base",
            IsFavorite = true,
            Resources = [new CreativeProjectResource { Name = "draft.txt", Kind = CreativeProjectResourceKind.File }]
        };
        var baseServices = new BaseExperienceServices(
            todos,
            focus,
            () => [project],
            () => [new CustomApp { Name = "Cursor", CreativeProjectId = "p1" }],
            () => [],
            () => DateTimeOffset.UtcNow);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: baseServices);

        var prepared = await executor.ExecuteAsync(AssistantToolNames.WorkspacePrepare, """{"intent":"Secret Base"}""");
        Assert.True(prepared.Succeeded);
        Assert.False(prepared.ShouldLaunch);
        Assert.Contains("Secret Base", prepared.ContentForModel, StringComparison.Ordinal);

        var focusResult = await executor.ExecuteAsync(AssistantToolNames.FocusStart, """{"minutes":25}""");
        Assert.True(focusResult.Succeeded);
        Assert.True(focus.Current.IsRunning);

        var cleanup = await executor.ExecuteAsync(AssistantToolNames.FilesSuggestCleanup, "{}");
        Assert.True(cleanup.Succeeded);
        Assert.Contains("will not delete", cleanup.ContentForModel, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(ActionPrivilege.UserConfirmationRequired, BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.FilesDelete)!.RiskLevel);
    }
}

public class AssistantPlannerContinueTests
{
    [Fact]
    public void ContinueIntent_PreparesThenConfirms()
    {
        var plan = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.ActionRequest,
            "Secret Baseの開発を続けたい",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(plan);
        Assert.Contains(plan!.Steps, s => s.ToolName == AssistantToolNames.WorkspacePrepare);
        Assert.Contains(plan.Steps, s => s.ToolName == AssistantToolNames.WorkspaceContinue && s.RequiresConfirmation);
    }
}
