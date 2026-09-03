using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Intent;
using SecretBase.Core.Memory;
using SecretBase.Core.Search;
using SecretBase.Core.Security;
using SecretBase.Core.State;
using SecretBase.Core.Todo;
using SecretBase.Core.Widgets;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class MemoryStoreTests
{
    [Fact]
    public void RememberAndRecall_ByScopeAndQuery()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var store = new MemoryStore();
        store.Remember(new MemoryEntry
        {
            Scope = MemoryScope.Project,
            Key = "current-project",
            Summary = "Secret Base",
            Detail = "Personal AI OS",
            ProjectName = "Secret Base",
            Importance = MemoryImportance.High,
            Confidence = 0.9,
            CreatedAt = now,
            LastAccessedAt = now,
            ExpiresAt = now.AddDays(30)
        });

        var hits = store.Recall(now, scope: MemoryScope.Project, query: "Secret");
        Assert.Single(hits);
        Assert.Equal("Secret Base", hits[0].Summary);
    }

    [Fact]
    public void ExpiredEntries_ArePruned()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new MemoryStore();
        store.Remember(new MemoryEntry
        {
            Key = "old",
            Summary = "stale",
            CreatedAt = now.AddDays(-2),
            LastAccessedAt = now.AddDays(-2),
            ExpiresAt = now.AddDays(-1)
        });
        Assert.Empty(store.Recall(now));
    }

    [Fact]
    public void SensitivePayload_IsRefused()
    {
        var store = new MemoryStore();
        Assert.Throws<InvalidOperationException>(() => store.Remember(new MemoryEntry
        {
            Key = "secret",
            Summary = "sk-test-key"
        }));
        Assert.Throws<InvalidOperationException>(() => store.Remember(new MemoryEntry
        {
            Key = "path",
            Summary = @"C:\Users\me\secret.txt"
        }));
    }

    [Fact]
    public void Importance_KeepsHighWhenOverCap()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new MemoryStore();
        for (var i = 0; i < MemoryPolicy.MaxEntries + 5; i++)
        {
            store.Remember(new MemoryEntry
            {
                Key = "k" + i,
                Summary = "note " + i,
                Importance = MemoryImportance.Low,
                CreatedAt = now,
                LastAccessedAt = now
            });
        }

        store.Remember(new MemoryEntry
        {
            Key = "keep",
            Summary = "important decision",
            Importance = MemoryImportance.Critical,
            CreatedAt = now,
            LastAccessedAt = now
        });
        var kept = store.Recall(now, query: "important");
        Assert.Contains(kept, item => item.Key == "keep");
        Assert.True(store.Snapshot().Entries.Count <= MemoryPolicy.MaxEntries);
    }
}

public class ActivityAggregationTests
{
    [Fact]
    public void GroupsProjectWork_IntoMeaningfulActivity()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var log = new ActivityLog();
        log.Record(new ActivityEvent
        {
            Kind = ActivityKind.ApplicationOpened,
            Title = "IDE opened",
            ProjectName = "Secret Base",
            At = now
        });
        log.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Project opened",
            ProjectName = "Secret Base",
            At = now.AddMinutes(1)
        });
        log.Record(new ActivityEvent
        {
            Kind = ActivityKind.FileOpened,
            Title = "README opened",
            ProjectName = "Secret Base",
            At = now.AddMinutes(2)
        });
        log.Record(new ActivityEvent
        {
            Kind = ActivityKind.GitStateNoted,
            Title = "Git noted",
            ProjectName = "Secret Base",
            At = now.AddMinutes(3)
        });

        var meaningful = log.Meaningful(now.AddMinutes(4));
        Assert.Contains(meaningful, item => item.Title == "Worked on Secret Base");
        Assert.Contains(meaningful, item => item.Summary.Contains("project opened", StringComparison.Ordinal));
    }
}

public class UserStateComposerTests
{
    [Fact]
    public void RaisesConfidence_WhenWorkspaceAndCalendarAlign()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 10, 0, TimeSpan.Zero);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Secret Base Development", ProjectName = "Secret Base", PreparedAt = now },
            focus: null,
            [
                new CalendarEvent
                {
                    Title = "Secret Base Development",
                    Start = now.AddMinutes(-10),
                    End = now.AddHours(2)
                }
            ],
            new TodoList { Items = [TodoItem.Create("Windows manual QA")] },
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = now.AddHours(-1), EndedAt = now }],
            music: null,
            provider: new AssistantProviderStatusInfo { ProviderId = AssistantProviderIds.Local, IsConfigured = true });
        Assert.Equal("Good afternoon", state.Greeting);
        Assert.Equal("Secret Base", state.CurrentProjectName);
        Assert.True(state.Confidence >= 0.7);
    }
}

public class IntentEngineTests
{
    [Fact]
    public void ContinueProject_WhenWorkBlockAndProjectPresent()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Secret Base Development", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList(),
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = now, EndedAt = now }],
            null,
            null);
        var intent = IntentEngine.Detect(state);
        Assert.Equal(DetectedIntentKind.ContinueProject, intent.Kind);
        Assert.True(intent.IsActionable);
        Assert.True(intent.Confidence >= IntentEngine.ActionThreshold);
    }

    [Fact]
    public void Unknown_WhenNoSignal()
    {
        var state = UserStateComposer.Compose(
            DateTimeOffset.UtcNow,
            null,
            null,
            [],
            new TodoList(),
            [],
            null,
            null);
        var intent = IntentEngine.Detect(state);
        Assert.Equal(DetectedIntentKind.Unknown, intent.Kind);
        Assert.False(intent.IsActionable);
        Assert.True(intent.Confidence < IntentEngine.ActionThreshold);
    }

    [Fact]
    public void Utterance_StartFocus()
    {
        var state = UserStateComposer.Compose(
            DateTimeOffset.UtcNow, null, null, [], new TodoList(), [], null, null);
        var intent = IntentEngine.Detect(state, "Start pomodoro");
        Assert.Equal(DetectedIntentKind.StartFocus, intent.Kind);
    }
}

public class ProjectContinuationTests
{
    [Fact]
    public void CombinesMemoryActivityAndTodo_WithoutGit()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new UserState
        {
            Now = now,
            CurrentProjectName = "Secret Base",
            CurrentProjectId = "p1",
            Confidence = 0.87
        };
        var context = ProjectContinuation.Build(
            state,
            new WorkspaceSession
            {
                Title = "Secret Base Development",
                ProjectName = "Secret Base",
                LastSessionSummary = "Windows AutoStart QA",
                NextTask = "Windows manual QA",
                SuggestedFileNames = ["manual QA documentation"],
                PreparedAt = now
            },
            [
                new MemoryEntry
                {
                    Scope = MemoryScope.Decision,
                    Summary = "Use confirmation before application launch"
                }
            ],
            [new MeaningfulActivity { Title = "PR #28", StartedAt = now, EndedAt = now }],
            new TodoList { Items = [TodoItem.Create("Windows manual QA")] });
        var text = context.Format();
        Assert.Contains("Secret Base", text, StringComparison.Ordinal);
        Assert.Contains("Windows AutoStart QA", text, StringComparison.Ordinal);
        Assert.Contains("will not run git", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", text, StringComparison.OrdinalIgnoreCase);
    }
}

public class AutomationEngineTests
{
    [Fact]
    public void SuggestsContinue_WithConfirmation_WhenCalendarWorkMatches()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Secret Base Development", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList(),
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = now, EndedAt = now }],
            null,
            null);
        var intent = IntentEngine.Detect(state);
        var suggestion = AutomationEngine.Evaluate(state, intent, trigger: AutomationTriggerKind.Calendar);
        Assert.NotNull(suggestion);
        Assert.True(suggestion!.RequiresConfirmation);
        Assert.Equal(AutomationSafetyLevel.SafeAuto, suggestion.Safety);
        Assert.Contains("ready", suggestion.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaysQuiet_DuringFocus_EvenWithWorkBlock()
    {
        var now = DateTimeOffset.UtcNow;
        var focus = new FocusSessionStore().Start(now);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = now },
            focus,
            [new CalendarEvent { Title = "開発", Start = now, End = now.AddHours(1) }],
            new TodoList(),
            [],
            null,
            null);
        var intent = IntentEngine.Detect(state);
        Assert.Null(AutomationEngine.Evaluate(state, intent, trigger: AutomationTriggerKind.Calendar));
    }

    [Fact]
    public void LowConfidenceUnknown_DoesNotSuggest()
    {
        var state = UserStateComposer.Compose(
            DateTimeOffset.UtcNow, null, null, [], new TodoList(), [], null, null);
        var intent = IntentEngine.Detect(state);
        Assert.Null(AutomationEngine.Evaluate(state, intent));
    }

    [Fact]
    public void DismissedFeedback_LowersFutureConfidence()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var feedback = new AutomationFeedbackStore();
        for (var i = 0; i < 8; i++)
        {
            feedback.Record(new AutomationFeedback
            {
                Intent = DetectedIntentKind.ContinueProject,
                Accepted = false,
                At = now
            });
        }

        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList(),
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = now, EndedAt = now }],
            null,
            null);
        var intent = IntentEngine.Detect(state);
        Assert.True(feedback.AcceptanceRate(DetectedIntentKind.ContinueProject) < 0.2);
        var suggestion = AutomationEngine.Evaluate(state, intent, feedback, AutomationTriggerKind.Time);
        if (suggestion is not null)
        {
            Assert.True(suggestion.Confidence < intent.Confidence);
        }
    }

    [Fact]
    public void AcceptFeedback_IsStored()
    {
        var store = new AutomationFeedbackStore();
        store.Record(new AutomationFeedback { Intent = DetectedIntentKind.ContinueProject, Accepted = true });
        store.Record(new AutomationFeedback { Intent = DetectedIntentKind.ContinueProject, Accepted = true });
        Assert.Equal(1, store.AcceptanceRate(DetectedIntentKind.ContinueProject));
    }
}

public class BaseSearchTests
{
    [Fact]
    public void FindsTodoAndRelativeActivity()
    {
        var hits = BaseSearch.Query(
            "最近作ったTodo",
            [],
            [new ActivityEvent { Title = "Added review task", At = DateTimeOffset.UtcNow }],
            [new CreativeProject { Name = "Secret Base" }],
            [],
            new TodoList { Items = [TodoItem.Create("最近作ったTodo")] },
            null);
        Assert.Contains(hits, hit => hit.Kind == "todo");
    }
}

public class FileIntelligenceClassificationTests
{
    [Fact]
    public void ClassifiesTemporaryDuplicateAndImportant()
    {
        var now = DateTimeOffset.UtcNow;
        var project = new CreativeProject
        {
            Name = "Secret Base",
            Resources =
            [
                new CreativeProjectResource { Name = "tmp-notes.bak", Kind = CreativeProjectResourceKind.File },
                new CreativeProjectResource { Name = "README.md", Kind = CreativeProjectResourceKind.File },
                new CreativeProjectResource { Name = "shared.txt", Kind = CreativeProjectResourceKind.File }
            ],
            RecentItems =
            [
                new CreativeProjectRecentItem { Name = "README.md", OpenedAt = now }
            ]
        };
        var other = new CreativeProject
        {
            Name = "Other",
            Resources = [new CreativeProjectResource { Name = "shared.txt", Kind = CreativeProjectResourceKind.File }]
        };
        var classified = FileIntelligence.Classify([project, other], now, "Secret Base");
        Assert.Contains(classified, c => c.Name == "tmp-notes.bak" && c.Kind == FileCandidateKind.Temporary);
        Assert.Contains(classified, c => c.Name == "README.md" && c.Kind == FileCandidateKind.Important);
        Assert.Contains(classified, c => c.Name == "shared.txt" && c.Kind == FileCandidateKind.Duplicate);
        Assert.DoesNotContain(classified, c => (c.Reason + c.Name).Contains(@"C:\", StringComparison.OrdinalIgnoreCase));
    }
}

public class UserModelBuilderTests
{
    [Fact]
    public void TracksFrequentProjects_NotPersonality()
    {
        var model = UserModelBuilder.From(
            [
                new ActivityEvent { Kind = ActivityKind.ProjectOpened, ProjectName = "Secret Base", At = DateTimeOffset.UtcNow },
                new ActivityEvent { Kind = ActivityKind.ProjectOpened, ProjectName = "Secret Base", At = DateTimeOffset.UtcNow },
                new ActivityEvent { Kind = ActivityKind.ApplicationOpened, Title = "Cursor", At = DateTimeOffset.UtcNow }
            ],
            [],
            [new CustomApp { Name = "Cursor" }]);
        Assert.Contains("Secret Base", model.FrequentProjects);
        Assert.Contains("Cursor", model.FrequentApps);
    }
}

public class BaseDashboardComposerTests
{
    [Fact]
    public void EveningCard_ShowsContinuationWithoutSpamming()
    {
        var now = new DateTimeOffset(2026, 9, 3, 23, 48, 0, TimeSpan.Zero);
        var state = new UserState
        {
            Now = now,
            Greeting = "Good evening",
            CurrentProjectName = "Secret Base",
            OpenTodoCount = 3,
            ProviderLine = "Base AI ● Local",
            Confidence = 0.8
        };
        var card = BaseDashboardComposer.Compose(
            state,
            new ProjectContinuationContext
            {
                ProjectName = "Secret Base Development",
                LastSession = "AutoStart QA"
            },
            suggestion: null,
            calendarCount: 2);
        Assert.Equal("23:48", card.Time);
        Assert.Equal("GOOD EVENING", card.Greeting);
        Assert.Contains("AutoStart QA", card.ContinuationDetail, StringComparison.Ordinal);
        Assert.True(card.ShowContinue);
        Assert.Equal("2 events", card.CalendarLine);
        Assert.Equal("3 remaining", card.TasksLine);
    }
}

public class PersonalIntelligenceToolTests
{
    [Fact]
    public async Task MemorySearchStateAndFeedback_StaySafe()
    {
        var todos = new MemoryTodoStore();
        var focus = new FocusSessionStore();
        var project = new CreativeProject { Id = "p1", Name = "Secret Base", IsFavorite = true };
        var services = new BaseExperienceServices(
            todos,
            focus,
            () => [project],
            () => [],
            () => [],
            () => DateTimeOffset.UtcNow);
        var executor = new AssistantToolExecutor(
            BuiltinAssistantToolRegistry.Instance,
            baseExperience: services);

        var remembered = await executor.ExecuteAsync(
            AssistantToolNames.MemoryRemember,
            """{"key":"last-session","summary":"Windows AutoStart QA"}""");
        Assert.True(remembered.Succeeded);

        var recalled = await executor.ExecuteAsync(AssistantToolNames.MemoryRecall, """{"query":"AutoStart"}""");
        Assert.Contains("Windows AutoStart QA", recalled.ContentForModel, StringComparison.Ordinal);

        var search = await executor.ExecuteAsync(AssistantToolNames.SearchBase, """{"query":"Secret Base"}""");
        Assert.True(search.Succeeded);

        var state = await executor.ExecuteAsync(AssistantToolNames.UserState, "{}");
        Assert.Contains("Intent is not an action", state.ContentForModel, StringComparison.Ordinal);

        var secret = await executor.ExecuteAsync(
            AssistantToolNames.MemoryRemember,
            """{"key":"k","summary":"sk-live-secret"}""");
        Assert.False(secret.Succeeded);

        Assert.Equal(
            ActionPrivilege.UserConfirmationRequired,
            BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.FilesDelete)!.RiskLevel);
        Assert.Null(BuiltinAssistantToolRegistry.Instance.Find("shell.run"));
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(
            BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.AutomationFeedback)!));
        Assert.False(AssistantConfirmationPolicy.CanAutoExecute(
            BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.WorkspaceContinue)!));
    }
}

public class AssistantPlannerSearchTests
{
    [Fact]
    public void RelativeSearch_UsesSearchBase()
    {
        var plan = AssistantPlanner.TryBuildFromIntent(
            AssistantIntentKind.Question,
            "昨日見ていたGitHubのやつ",
            snapshot: null,
            maxSteps: 5);
        Assert.NotNull(plan);
        Assert.Contains(plan!.Steps, step => step.ToolName == AssistantToolNames.SearchBase);
    }
}

public class DashboardCatalogTests
{
    [Fact]
    public void DashboardIsCatalogOnly_NotDefaultLayout()
    {
        Assert.Contains(WidgetCatalog.Entries, e => e.WidgetType == WidgetTypes.Dashboard);
        Assert.DoesNotContain(DesktopLayout.CreateDefault().Widgets, w => w.Type == WidgetTypes.Dashboard);
        Assert.Equal(2, DesktopLayout.CreateDefault().Widgets.Count);
    }
}

public class WorkspaceRestoreTests
{
    [Fact]
    public void Prepare_UsesSessionMemory_AndRestoreKeepsNamesOnly()
    {
        var session = WorkspacePreparer.Prepare(
            "Continue Secret Base",
            [new CreativeProject { Id = "p1", Name = "Secret Base", IsFavorite = true }],
            [new CustomApp { Name = "Cursor", CreativeProjectId = "p1" }],
            new TodoList { Items = [TodoItem.Create("Windows manual QA")] },
            [],
            DateTimeOffset.UtcNow,
            [new MemoryEntry { Scope = MemoryScope.Session, Summary = "Windows AutoStart QA" }]);
        Assert.Equal("Windows AutoStart QA", session.LastSessionSummary);
        Assert.Contains("Cursor", session.SuggestedAppNames);
        Assert.DoesNotContain("powershell", session.LastSessionSummary, StringComparison.OrdinalIgnoreCase);
    }
}
