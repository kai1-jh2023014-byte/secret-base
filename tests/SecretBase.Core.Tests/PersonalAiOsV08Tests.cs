using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Intent;
using SecretBase.Core.Memory;
using SecretBase.Core.Observation;
using SecretBase.Core.Search;
using SecretBase.Core.Security;
using SecretBase.Core.Session;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class ObservationSanitizerTests
{
    [Fact]
    public void DropsBrowserTitles_UnlessTheyNameARegisteredProject()
    {
        Assert.Null(ObservationSanitizer.SafeWindowTitle(
            "Inbox - Gmail",
            "chrome",
            ["Secret Base"]));
        Assert.Equal(
            "Secret Base - Google Chrome",
            ObservationSanitizer.SafeWindowTitle("Secret Base - Google Chrome", "chrome", ["Secret Base"]));
    }

    [Fact]
    public void RefusesSecretsAndPaths()
    {
        Assert.Null(ObservationSanitizer.SafeWindowTitle("password reset", "notepad", []));
        Assert.Null(ObservationSanitizer.SafeWindowTitle(@"C:\Users\me\secret.txt", "notepad", []));
        Assert.Null(ObservationSanitizer.SafeApplicationName("sk-live-secret"));
        Assert.Equal("Cursor", ObservationSanitizer.SafeApplicationName("Cursor.exe"));
    }
}

public class ObservationNormalizerTests
{
    [Fact]
    public void ChromeWithoutProject_IsDropped()
    {
        var mapped = ObservationNormalizer.ToActivity(
            new ObservationEvent
            {
                Kind = ObservationKind.ApplicationActivated,
                ApplicationName = "chrome",
                WindowTitle = null,
                At = DateTimeOffset.UtcNow
            },
            [],
            []);
        Assert.Null(mapped);
    }

    [Fact]
    public void RegisteredApp_BecomesApplicationOpened()
    {
        var mapped = ObservationNormalizer.ToActivity(
            new ObservationEvent
            {
                Kind = ObservationKind.ApplicationActivated,
                ApplicationName = "Cursor",
                WindowTitle = "IntentEngine.cs",
                At = DateTimeOffset.UtcNow
            },
            [new CustomApp { Name = "Cursor" }],
            [new CreativeProject { Name = "Secret Base" }]);
        Assert.NotNull(mapped);
        Assert.Equal(ActivityKind.ApplicationOpened, mapped!.Kind);
        Assert.Equal("Cursor", mapped.Title);
    }

    [Fact]
    public void IdleAndDedup()
    {
        var now = DateTimeOffset.UtcNow;
        var idle = ObservationNormalizer.ToActivity(
            new ObservationEvent { Kind = ObservationKind.IdleStarted, At = now },
            [],
            []);
        Assert.Equal(ActivityKind.IdleStarted, idle!.Kind);
        Assert.Null(ObservationNormalizer.ToActivity(
            new ObservationEvent { Kind = ObservationKind.IdleStarted, At = now.AddSeconds(10) },
            [],
            [],
            idle));
    }
}

public class MemoryRankerTests
{
    [Fact]
    public void RanksRelevantProjectAboveOldLowImportance()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var ranked = MemoryRanker.Rank(
            [
                new MemoryEntry
                {
                    Key = "old",
                    Summary = "Unrelated note",
                    ProjectName = "Other",
                    Importance = MemoryImportance.Low,
                    Confidence = 0.4,
                    CreatedAt = now.AddDays(-20),
                    UpdatedAt = now.AddDays(-20)
                },
                new MemoryEntry
                {
                    Key = "current",
                    Summary = "Intent Engine evidence ranking",
                    ProjectName = "Secret Base",
                    Importance = MemoryImportance.High,
                    Confidence = 0.9,
                    CreatedAt = now,
                    UpdatedAt = now
                }
            ],
            now,
            query: "Intent",
            projectName: "Secret Base",
            take: 2);
        Assert.Equal("current", ranked[0].Key);
    }

    [Fact]
    public void ForgetAndExpiry_RemoveEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new MemoryStore();
        var kept = store.Remember(new MemoryEntry
        {
            Key = "keep",
            Summary = "workspace Base AI",
            CreatedAt = now,
            ExpiresAt = now.AddDays(1)
        });
        store.Remember(new MemoryEntry
        {
            Key = "old",
            Summary = "stale session",
            CreatedAt = now.AddDays(-2),
            ExpiresAt = now.AddMinutes(-1)
        });
        Assert.Single(store.Recall(now));
        store.Forget(kept.Id);
        Assert.Empty(store.Recall(now));
    }
}

public class WorkSessionTests
{
    [Fact]
    public void StartTouchEnd_ProducesDeterministicSummary()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var store = new WorkSessionStore();
        store.StartOrContinue(now, "p1", "Secret Base", "Base AI");
        store.Touch("Intent Engine", "IntentEngine.cs", "Evidence-based intent ranking", focusStarted: true);
        var ended = store.End(now.AddHours(2));
        Assert.NotNull(ended);
        Assert.Contains("Secret Base", ended!.Summary, StringComparison.Ordinal);
        Assert.Contains("Intent Engine", ended.Summary, StringComparison.Ordinal);
        Assert.Contains("Evidence-based intent ranking", ended.Summary, StringComparison.Ordinal);
        Assert.Equal(1, ended.FocusCount);
        Assert.Contains("IntentEngine.cs", ended.OpenedResources);
        Assert.Null(store.Current);
        Assert.Single(store.Recent(1));
    }
}

public class SituationComposerTests
{
    [Fact]
    public void ComposesEvidence_FromCalendarTodoSessionAndActivity()
    {
        var now = new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Base AI", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList { Items = [TodoItem.Create("Finish intent engine")] },
            [new MeaningfulActivity { Title = "IntentEngine.cs", ProjectName = "Secret Base", StartedAt = now, EndedAt = now }],
            null,
            null);
        var situation = SituationComposer.Compose(
            state,
            [new MeaningfulActivity { Title = "IntentEngine.cs", ProjectName = "Secret Base", StartedAt = now, EndedAt = now }],
            [new MemoryEntry { Scope = MemoryScope.Session, Summary = "Previous workspace = Base AI" }],
            new WorkSession { ProjectName = "Secret Base", WorkspaceTitle = "Base AI", Summary = "Secret Base: Intent Engine" },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList { Items = [TodoItem.Create("Finish intent engine")] });
        Assert.Equal("Secret Base", situation.ProjectName);
        Assert.False(situation.Focus);
        Assert.True(situation.Confidence >= 0.7);
        Assert.Contains(situation.Evidence, item => item.Source == "calendar");
        Assert.Contains(situation.Evidence, item => item.Source == "todo");
        Assert.Contains("Finish intent engine", situation.Format(), StringComparison.Ordinal);
    }
}

public class EvidenceIntentTests
{
    [Fact]
    public void ResumePreviousSession_FromUtteranceAndFromLastSession()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var empty = UserStateComposer.Compose(now, null, null, [], new TodoList(), [], null, null);
        var spoken = IntentEngine.Detect(empty, "昨日の続きをやりたい");
        Assert.Equal(DetectedIntentKind.ResumePreviousSession, spoken.Kind);
        Assert.NotEmpty(spoken.Evidence);

        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Base AI", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [],
            new TodoList { Items = [TodoItem.Create("Implement evidence ranking")] },
            [new MeaningfulActivity { Title = "Intent Engine", StartedAt = now.AddHours(-2), EndedAt = now.AddHours(-1) }],
            null,
            null);
        var situation = SituationComposer.Compose(state);
        var session = new WorkSession
        {
            ProjectName = "Secret Base",
            WorkspaceTitle = "Base AI",
            PrimaryActivity = "Intent Engine",
            Summary = "Secret Base: Intent Engine"
        };
        var intent = IntentEngine.Detect(state, situation, session);
        Assert.Equal(DetectedIntentKind.ResumePreviousSession, intent.Kind);
        Assert.True(intent.Confidence >= IntentEngine.ActionThreshold);
        Assert.Contains(intent.Evidence, item => item.Contains("Previous session", StringComparison.Ordinal));
    }

    [Fact]
    public void ContinueProject_CarriesEvidenceAndIsNotAnAction()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var state = UserStateComposer.Compose(
            now,
            new WorkspaceSession { Title = "Secret Base Development", ProjectName = "Secret Base", PreparedAt = now },
            null,
            [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }],
            new TodoList { Items = [TodoItem.Create("Windows manual QA")] },
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = now, EndedAt = now }],
            null,
            null);
        var intent = IntentEngine.Detect(state, SituationComposer.Compose(state));
        Assert.Equal(DetectedIntentKind.ContinueProject, intent.Kind);
        Assert.True(intent.IsActionable);
        Assert.NotEmpty(intent.Evidence);
        Assert.True(intent.At == now);
    }
}

public class AutomationPipelineTests
{
    [Fact]
    public void QuietHoursAndFocus_StaySilent()
    {
        var night = new DateTimeOffset(2026, 9, 3, 23, 15, 0, TimeSpan.Zero);
        var nightState = UserStateComposer.Compose(
            night,
            new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = night },
            null,
            [],
            new TodoList(),
            [new MeaningfulActivity { Title = "Worked on Secret Base", StartedAt = night, EndedAt = night }],
            null,
            null);
        var nightIntent = IntentEngine.Detect(nightState);
        Assert.Equal(InterventionMode.Silent, AutomationEngine.Run(nightState, nightIntent).Mode);

        var day = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var focus = new FocusSessionStore().Start(day);
        var focusState = UserStateComposer.Compose(
            day,
            new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = day },
            focus,
            [new CalendarEvent { Title = "開発", Start = day, End = day.AddHours(1) }],
            new TodoList(),
            [],
            null,
            null);
        Assert.Equal(
            InterventionMode.Silent,
            AutomationEngine.Run(focusState, IntentEngine.Detect(focusState), trigger: AutomationTriggerKind.Calendar).Mode);
    }

    [Fact]
    public void ConsecutiveDismissals_BecomePassive_AndAcceptsNeverDropConfirmation()
    {
        Assert.False(LearningPolicy.MayEscalatePrivilege);
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
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

        var dismissed = new AutomationFeedbackStore();
        for (var i = 0; i < 3; i++)
        {
            dismissed.Record(new AutomationFeedback { Intent = DetectedIntentKind.ContinueProject, Accepted = false, At = now });
        }

        var afterDismiss = AutomationEngine.Run(state, intent, dismissed, AutomationTriggerKind.Calendar);
        Assert.Equal(InterventionMode.Passive, afterDismiss.Mode);
        Assert.Null(afterDismiss.Suggestion);

        var accepted = new AutomationFeedbackStore();
        for (var i = 0; i < 20; i++)
        {
            accepted.Record(new AutomationFeedback { Intent = DetectedIntentKind.ContinueProject, Accepted = true, At = now });
        }

        var afterAccept = AutomationEngine.Run(state, intent, accepted, AutomationTriggerKind.Calendar);
        Assert.NotNull(afterAccept.Suggestion);
        Assert.True(afterAccept.Suggestion!.RequiresConfirmation);
        Assert.Equal(AutomationSafetyLevel.SafeAuto, afterAccept.Suggestion.Safety);
        Assert.True(afterAccept.Suggestion.Confidence >= intent.Confidence * 0.7);
    }
}

public class SearchSessionTests
{
    [Fact]
    public void FindsSessionsAndKeepsMetadata()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var hits = BaseSearch.Query(
            "Intent Engine",
            [],
            [],
            [new CreativeProject { Name = "Secret Base" }],
            [],
            new TodoList(),
            new WorkspaceSession { Title = "Base AI", LastSessionSummary = "Intent Engine", PreparedAt = now },
            [new WorkSession { Summary = "Secret Base: Intent Engine", ProjectName = "Secret Base", StartedAt = now }]);
        Assert.Contains(hits, hit => hit.Kind == "session" && hit.At == now && hit.Source == "session");
        Assert.Contains(hits, hit => hit.Kind == "workspace");
    }
}

public class FileIntelligenceProjectRelatedTests
{
    [Fact]
    public void CurrentProjectRecentFile_IsProjectRelated()
    {
        var now = DateTimeOffset.UtcNow;
        var classified = FileIntelligence.Classify(
            [
                new CreativeProject
                {
                    Name = "Secret Base",
                    Resources = [new CreativeProjectResource { Name = "IntentEngine.cs", Kind = CreativeProjectResourceKind.File }],
                    RecentItems = [new CreativeProjectRecentItem { Name = "IntentEngine.cs", OpenedAt = now }]
                }
            ],
            now,
            currentProjectName: "Secret Base");
        Assert.Contains(classified, item => item.Name == "IntentEngine.cs" && item.Kind == FileCandidateKind.ProjectRelated);
    }
}

public class PersonalOsE2ETests
{
    private static BaseExperienceServices CreateServices(
        DateTimeOffset now,
        IReadOnlyList<CreativeProject>? projects = null,
        IReadOnlyList<CustomApp>? apps = null,
        IReadOnlyList<CalendarEvent>? events = null,
        IWorkSessionStore? sessions = null)
    {
        return new BaseExperienceServices(
            new MemoryTodoStore(),
            new FocusSessionStore(),
            () => projects ?? [new CreativeProject { Id = "p1", Name = "Secret Base", IsFavorite = true }],
            () => apps ?? [new CustomApp { Name = "Cursor" }],
            () => events ?? [],
            () => now,
            sessions: sessions);
    }

    [Fact]
    public void ScenarioA_StartupLoadsState_WithoutLlm()
    {
        var now = new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero);
        var services = CreateServices(now);
        var execution = services.EvaluatePipeline(AutomationTriggerKind.Startup);
        Assert.NotNull(execution.Result);
        Assert.NotNull(services.ComposeSituation());
        Assert.Contains("AI outage", BaseAiCatalog.AllowedSlices, StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioB_Resume_RecallsPreviousSession()
    {
        var now = new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero);
        var sessions = new WorkSessionStore();
        var services = CreateServices(now, sessions: sessions);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            Title = "Base AI",
            ProjectId = "p1",
            ProjectName = "Secret Base",
            LastSessionSummary = "Intent Engine",
            NextTask = "Evidence-based intent ranking",
            PreparedAt = now
        });
        var ended = sessions.End(now.AddHours(2));
        Assert.Contains("Intent Engine", ended!.Summary, StringComparison.Ordinal);
        var continuation = services.Continuation();
        Assert.Equal("Secret Base", continuation.ProjectName);
        Assert.Contains("Intent Engine", continuation.LastSession, StringComparison.Ordinal);
        Assert.Contains("Evidence-based intent ranking", continuation.NextTask, StringComparison.Ordinal);
        Assert.Contains("will not run git", continuation.Format(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScenarioC_TimeAwareness_ContinueProject()
    {
        var now = new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);
        var services = CreateServices(
            now,
            events:
            [
                new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }
            ]);
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Secret Base",
            ProjectName = "Secret Base",
            At = now.AddMinutes(-20)
        });
        services.CurrentWorkspace = new WorkspaceSession
        {
            Title = "Base AI",
            ProjectName = "Secret Base",
            PreparedAt = now
        };
        var intent = services.DetectIntent();
        Assert.Equal(DetectedIntentKind.ContinueProject, intent.Kind);
        Assert.True(intent.Confidence >= 0.7);
        var execution = services.EvaluatePipeline(AutomationTriggerKind.Calendar);
        Assert.True(execution.Mode is InterventionMode.Confirm or InterventionMode.Suggest);
        Assert.NotNull(execution.Suggestion);
        Assert.True(execution.Suggestion!.RequiresConfirmation);
    }

    [Fact]
    public void ScenarioD_Focus_NoInterruption()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var services = CreateServices(
            now,
            events: [new CalendarEvent { Title = "開発", Start = now, End = now.AddHours(1) }]);
        services.CurrentWorkspace = new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = now };
        services.Focus.Start(now);
        var execution = services.EvaluatePipeline(AutomationTriggerKind.Calendar);
        Assert.Equal(InterventionMode.Silent, execution.Mode);
        Assert.Null(execution.Suggestion);
    }

    [Fact]
    public void ScenarioE_CrossApp_BuildsSituationAndIntent()
    {
        var now = new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);
        var todos = new MemoryTodoStore();
        todos.Save(new TodoList { Items = [TodoItem.Create("Finish Intent Engine")] });
        var services = new BaseExperienceServices(
            todos,
            new FocusSessionStore(),
            () => [new CreativeProject { Id = "p1", Name = "Secret Base" }],
            () => [],
            () => [new CalendarEvent { Title = "Secret Base development", Start = now, End = now.AddHours(2) }],
            () => now);
        services.Memory.Remember(new MemoryEntry
        {
            Scope = MemoryScope.Workspace,
            Key = "previous-workspace",
            Summary = "Previous workspace = Base AI",
            ProjectName = "Secret Base",
            CreatedAt = now
        });
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Secret Base",
            ProjectName = "Secret Base",
            At = now
        });
        services.CurrentWorkspace = new WorkspaceSession { Title = "Base AI", ProjectName = "Secret Base", PreparedAt = now };
        var situation = services.ComposeSituation();
        var intent = services.DetectIntent();
        Assert.Equal("Secret Base", situation.ProjectName);
        Assert.NotEmpty(situation.Evidence);
        Assert.Equal(DetectedIntentKind.ContinueProject, intent.Kind);
        Assert.True(intent.Confidence >= 0.7);
    }

    [Fact]
    public void ScenarioF_Learning_ChangesRanking_NotPrivilege()
    {
        var now = new DateTimeOffset(2026, 9, 3, 14, 0, 0, TimeSpan.Zero);
        var services = CreateServices(
            now,
            events: [new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }]);
        services.CurrentWorkspace = new WorkspaceSession { Title = "Dev", ProjectName = "Secret Base", PreparedAt = now };
        var first = AutomationEngine.Run(
            services.ComposeUserState(),
            services.DetectIntent(),
            services.AutomationFeedback,
            AutomationTriggerKind.Calendar);
        Assert.NotNull(first.Suggestion);
        var baseline = first.Suggestion!.Confidence;
        services.LastSuggestion = first.Suggestion;
        services.RecordFeedback(true);
        for (var i = 0; i < 8; i++)
        {
            services.AutomationFeedback.Record(new AutomationFeedback
            {
                Intent = DetectedIntentKind.ContinueProject,
                Accepted = true,
                At = now
            });
        }

        var accepted = AutomationEngine.Run(
            services.ComposeUserState(),
            services.DetectIntent(),
            services.AutomationFeedback,
            AutomationTriggerKind.Calendar);
        Assert.NotNull(accepted.Suggestion);
        Assert.True(accepted.Suggestion!.RequiresConfirmation);
        Assert.True(accepted.Suggestion.Confidence >= baseline);
        Assert.False(LearningPolicy.MayEscalatePrivilege);

        for (var i = 0; i < 3; i++)
        {
            services.AutomationFeedback.Record(new AutomationFeedback
            {
                Intent = DetectedIntentKind.ContinueProject,
                Accepted = false,
                At = now
            });
        }

        var dismissed = AutomationEngine.Run(
            services.ComposeUserState(),
            services.DetectIntent(),
            services.AutomationFeedback,
            AutomationTriggerKind.Calendar);
        Assert.Equal(InterventionMode.Passive, dismissed.Mode);
        Assert.Null(dismissed.Suggestion);
        var model = services.UserModel();
        Assert.True(model.SuggestionDismissals >= 3);
    }

    [Fact]
    public void ScenarioG_AiCatalog_DoesNotBlockCoreLoop()
    {
        var services = CreateServices(DateTimeOffset.UtcNow);
        Assert.Equal(DetectedIntentKind.Unknown, services.DetectIntent().Kind);
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Secret Base",
            ProjectName = "Secret Base",
            At = DateTimeOffset.UtcNow
        });
        Assert.NotEmpty(services.Search("recent"));
        Assert.Contains("Never send", BaseAiCatalog.Forbidden, StringComparison.Ordinal);
        Assert.Equal(
            ActionPrivilege.UserConfirmationRequired,
            BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.FilesDelete)!.RiskLevel);
        Assert.Null(BuiltinAssistantToolRegistry.Instance.Find("shell.run"));
        Assert.Null(BuiltinAssistantToolRegistry.Instance.Find("powershell.run"));
    }

    [Fact]
    public void ScenarioH_Observation_ReachesActivitySituationAndSession()
    {
        var now = new DateTimeOffset(2026, 9, 3, 11, 0, 0, TimeSpan.Zero);
        var services = CreateServices(now);
        var ingested = services.IngestObservation(new ObservationEvent
        {
            Kind = ObservationKind.ApplicationActivated,
            ApplicationName = "Cursor",
            WindowTitle = "Secret Base",
            At = now
        });
        Assert.True(ingested);
        Assert.Contains(services.Activity.Recent(), item => item.Title == "Cursor");
        Assert.Equal("Secret Base", services.ComposeSituation().ProjectName);
        Assert.NotNull(services.Sessions.Current);
        Assert.False(services.IngestObservation(new ObservationEvent
        {
            Kind = ObservationKind.ApplicationActivated,
            ApplicationName = "chrome",
            At = now.AddSeconds(1)
        }));
    }

    [Fact]
    public async Task SituationAndSessionTools_AreReadOnly()
    {
        var services = CreateServices(DateTimeOffset.UtcNow);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            Title = "Base AI",
            ProjectName = "Secret Base",
            LastSessionSummary = "Intent Engine",
            PreparedAt = DateTimeOffset.UtcNow
        });
        var executor = new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, baseExperience: services);
        var situation = await executor.ExecuteAsync(AssistantToolNames.SituationNow, "{}");
        Assert.True(situation.Succeeded);
        Assert.Contains("Project:", situation.ContentForModel, StringComparison.Ordinal);
        var sessions = await executor.ExecuteAsync(AssistantToolNames.SessionRecent, "{}");
        Assert.True(sessions.Succeeded);
        Assert.False(BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.SituationNow)!.RequiresConfirmation);
        Assert.False(BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.SessionRecent)!.RequiresConfirmation);
    }
}
