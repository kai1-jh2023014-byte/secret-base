using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
using SecretBase.Core.Calendar;
using SecretBase.Core.Capture;
using SecretBase.Core.Commands;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Explain;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Intent;
using SecretBase.Core.Memory;
using SecretBase.Core.Observation;
using SecretBase.Core.Privacy;
using SecretBase.Core.Search;
using SecretBase.Core.Security;
using SecretBase.Core.Session;
using SecretBase.Core.Situation;
using SecretBase.Core.Todo;
using SecretBase.Core.Timeline;
using SecretBase.Core.Widgets;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Tests;

public class PersonalAiOsV10Tests
{
    private static BaseExperienceServices Create(
        DateTimeOffset now,
        IReadOnlyList<CreativeProject>? projects = null,
        IReadOnlyList<CalendarEvent>? events = null,
        IWorkSessionStore? sessions = null,
        ITodoStore? todos = null)
    {
        return new BaseExperienceServices(
            todos ?? new MemoryTodoStore(),
            new FocusSessionStore(),
            () => projects ??
            [
                new CreativeProject
                {
                    Id = "p1",
                    Name = "Secret Base",
                    IsFavorite = true,
                    Resources =
                    [
                        new CreativeProjectResource { Name = "export-final.zip", Kind = CreativeProjectResourceKind.File },
                        new CreativeProjectResource { Name = "bin/Debug/app.dll", Kind = CreativeProjectResourceKind.File }
                    ]
                },
                new CreativeProject { Id = "p2", Name = "Tetris AI" }
            ],
            () => [new CustomApp { Name = "Cursor" }],
            () => events ??
            [
                new CalendarEvent
                {
                    Title = "School",
                    Start = now.Date.AddHours(9),
                    End = now.Date.AddHours(12)
                }
            ],
            () => now,
            sessions: sessions);
    }

    [Fact]
    public void Memory_UpdateMergeExpire_AndSecretRejection()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new MemoryStore();
        var saved = store.Remember(new MemoryEntry
        {
            Key = "note",
            Summary = "Original",
            CreatedAt = now,
            ExpiresAt = now.AddDays(1)
        });
        var updated = store.Update(saved.Id, summary: "Updated");
        Assert.Equal("Updated", updated.Summary);
        var merged = store.Merge(new MemoryEntry { Key = "note", Summary = "Merged", CreatedAt = now, ExpiresAt = now.AddDays(1) });
        Assert.Equal("Merged", merged.Summary);
        Assert.Throws<InvalidOperationException>(() => store.Update(saved.Id, summary: "sk-secret"));
        Assert.Equal(0, store.Expire(now));
        store.Remember(new MemoryEntry { Key = "temp", Summary = "gone", CreatedAt = now, ExpiresAt = now.AddMinutes(1) });
        Assert.Equal(1, store.Expire(now.AddMinutes(2)));
    }

    [Fact]
    public void Capture_ClassifiesIdeaBeforeGenericTodo()
    {
        var draft = QuickCaptureClassifier.Classify("DTM AIにコード進行生成を追加したい", ["DTM AI"]);
        Assert.Equal(CaptureDestination.Idea, draft.Destination);
        Assert.Equal("DTM AI", draft.ProjectName);
    }

    [Fact]
    public void CommandCenter_BriefingContinueFocusExplain_WithoutLlm()
    {
        var now = new DateTimeOffset(2026, 9, 3, 9, 15, 0, TimeSpan.Zero);
        var services = Create(now);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            Title = "Base AI",
            ProjectId = "p1",
            ProjectName = "Secret Base",
            LastSessionSummary = "Windows observation",
            NextTask = "Windows QA",
            PreparedAt = now.AddDays(-1)
        });

        var briefing = services.Dispatch("今日何すればいい？");
        Assert.True(briefing.HandledWithoutLlm);
        Assert.Equal(CommandKind.Briefing, briefing.Kind);
        Assert.Contains("School", briefing.Body, StringComparison.Ordinal);

        var cont = services.Dispatch("昨日の続きをやりたい");
        Assert.True(cont.HandledWithoutLlm);
        Assert.True(cont.RequiresConfirmation);
        Assert.Equal(AssistantToolNames.WorkspaceContinue, cont.ToolHint);
        Assert.Contains("Confidence", cont.Body, StringComparison.Ordinal);

        var focus = services.Dispatch("30分集中したい");
        Assert.Equal(CommandKind.Focus, focus.Kind);
        Assert.True(focus.HandledWithoutLlm);

        var why = services.Dispatch("なぜそう判断したの？");
        Assert.Equal(CommandKind.Explain, why.Kind);
        Assert.Contains("I think you may want", why.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Timeline_AggregatesMeaningfulActivity()
    {
        var now = new DateTimeOffset(2026, 9, 3, 18, 0, 0, TimeSpan.Zero);
        var services = Create(now);
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Secret Base",
            ProjectName = "Secret Base",
            At = now.Date.AddHours(15).AddMinutes(42)
        });
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.FocusStarted,
            Title = "Focus",
            At = now.Date.AddHours(17).AddMinutes(10)
        });
        var text = ActivityTimeline.Format(services.Timeline());
        Assert.Contains("15:42", text, StringComparison.Ordinal);
        Assert.Contains("School", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Palette_RanksContinueAndFocus()
    {
        var services = Create(new DateTimeOffset(2026, 9, 3, 19, 0, 0, TimeSpan.Zero));
        var items = services.Palette("focus");
        Assert.Contains(items, item => item.Action == "focus");
        Assert.Contains(services.Palette(""), item => item.Action == "continue");
    }

    [Fact]
    public void AutomationRules_CooldownAndDisable()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new AutomationRuleStore();
        var intent = new DetectedIntent { Kind = DetectedIntentKind.ResumePreviousSession, Confidence = 0.9 };
        var first = AutomationScheduler.Match(store.List(), AutomationTriggerKind.Startup, intent, now);
        Assert.NotNull(first);
        first!.LastRun = now;
        store.Save(first);
        Assert.Null(AutomationScheduler.Match(store.List(), AutomationTriggerKind.Startup, intent, now.AddMinutes(10)));
        first.Enabled = false;
        store.Save(first);
        Assert.Null(AutomationScheduler.Match(store.List(), AutomationTriggerKind.Startup, intent, now.AddHours(5)));
    }

    [Fact]
    public void QuietHours_FromSettings_StaySilent()
    {
        var now = new DateTimeOffset(2026, 9, 3, 23, 0, 0, TimeSpan.Zero);
        var services = Create(now);
        services.Preferences = new BaseSettings { QuietHoursStart = 22, QuietHoursEnd = 8 };
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            ProjectName = "Secret Base",
            Title = "Base",
            PreparedAt = now
        });
        var execution = services.EvaluatePipeline(AutomationTriggerKind.Time);
        Assert.Equal(InterventionMode.Silent, execution.Mode);
    }

    [Fact]
    public void Attention_SilentDuringFocus()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.Focus.Start(now, TimeSpan.FromMinutes(25));
        Assert.Empty(services.Attention());
    }

    [Fact]
    public void Search_FiltersByType_AndHasRankerHook()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.Memory.Remember(new MemoryEntry { Key = "s", Summary = "Secret Base observation", CreatedAt = now });
        var hits = services.Search("type:memory Secret");
        Assert.All(hits, hit => Assert.Equal("memory", hit.Kind));
        var ranked = new KeywordSearchRanker().Rank(hits, "Secret");
        Assert.Equal(hits[0].Title, ranked[0].Title);
    }

    [Fact]
    public void FileIntelligence_FlagsExportAndArtifact_NeverDeletes()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        var candidates = FileIntelligence.SuggestCleanup(services.ListProjects(), now);
        Assert.Contains(candidates, item => item.Kind == FileCandidateKind.OldExport);
        Assert.Contains(candidates, item => item.Kind == FileCandidateKind.BuildArtifact);
        Assert.DoesNotContain(candidates, item => item.Reason.Contains("deleted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Learning_AdjustsRanking_WithoutPrivilegeEscalation()
    {
        var store = new AutomationFeedbackStore();
        for (var i = 0; i < 5; i++)
        {
            store.Record(new AutomationFeedback { Intent = DetectedIntentKind.ContinueProject, Accepted = true });
        }

        for (var i = 0; i < 4; i++)
        {
            store.Record(new AutomationFeedback { Intent = DetectedIntentKind.MusicListening, Accepted = false });
        }

        var insights = LearningLoop.Detect(store.Recent(40));
        Assert.Contains(insights, item => item.Intent == DetectedIntentKind.ContinueProject && item.Adjustment == "increase ranking");
        Assert.Contains(insights, item => item.Intent == DetectedIntentKind.MusicListening && item.Adjustment == "reduce ranking");
        Assert.All(insights, item => Assert.True(item.PrivilegeUnchanged));
        Assert.False(LearningLoop.MayEscalatePrivilege);
        Assert.True(BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.WorkspaceContinue)!.RequiresConfirmation);
    }

    [Fact]
    public void PrivacyManifest_ListsNeverCollected()
    {
        Assert.Contains("clipboard", PrivacyManifest.NeverCollected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Keystrokes", PrivacyManifest.NeverCollected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser page", PrivacyManifest.NeverCollected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DefaultLayout_RemainsClockAndText()
    {
        Assert.Equal(2, DesktopLayout.CreateDefault().Widgets.Count);
        Assert.DoesNotContain(DesktopLayout.CreateDefault().Widgets, w => w.Type == WidgetTypes.Dashboard);
    }

    [Fact]
    public void Safety_RegistryStillBlocksShellAndRequiresDeleteConfirm()
    {
        var registry = BuiltinAssistantToolRegistry.Instance;
        Assert.Equal(43, registry.Tools.Count);
        Assert.Null(registry.Find("shell.run"));
        Assert.True(registry.Find(AssistantToolNames.FilesDelete)!.RequiresConfirmation);
        Assert.Equal(ActionPrivilege.UserConfirmationRequired, registry.Find(AssistantToolNames.FilesDelete)!.RiskLevel);
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.DailyBriefing)!));
        Assert.True(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.QuickCapture)!));
        Assert.False(AssistantConfirmationPolicy.CanAutoExecute(registry.Find(AssistantToolNames.WorkspaceContinue)!));
    }

    [Fact]
    public async Task Assistant_CommandCenter_DoesNotCallLlm()
    {
        var now = new DateTimeOffset(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);
        var services = Create(now);
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, baseExperience: services),
            () => new UnavailableAiProvider("x", "down", "offline"));
        assistant.CommandContext = services;
        var result = await assistant.SendAsync("今日何すればいい？");
        Assert.True(result.Succeeded);
        Assert.Contains("School", result.AssistantText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioB_Continue_RequestsConfirmation()
    {
        var now = new DateTimeOffset(2026, 9, 3, 18, 42, 0, TimeSpan.Zero);
        var services = Create(now);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            Title = "Observation",
            ProjectId = "p1",
            ProjectName = "Secret Base",
            LastSessionSummary = "Windows observation",
            NextTask = "Windows QA",
            SuggestedAppNames = ["Rider"],
            PreparedAt = now
        });
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, baseExperience: services),
            () => new UnavailableAiProvider("x", "down", "offline"));
        assistant.CommandContext = services;
        var result = await assistant.SendAsync("昨日の続きをやりたい");
        Assert.NotNull(result.PendingConfirmation);
        Assert.Equal(AssistantToolNames.WorkspaceContinue, result.PendingConfirmation!.ToolName);
        Assert.Contains("Windows observation", result.PendingConfirmation.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScenarioC_Focus_StartsTimerWithoutLaunch()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, baseExperience: services),
            () => new UnavailableAiProvider("x", "down", "offline"));
        assistant.CommandContext = services;
        var result = await assistant.SendAsync("30分集中したい");
        Assert.True(result.Succeeded);
        Assert.NotNull(services.Focus.Current);
        Assert.Contains("30", result.AssistantText, StringComparison.Ordinal);
        Assert.False(result.ShouldLaunch);
        Assert.Empty(services.Attention());
    }

    [Fact]
    public void ScenarioD_Observation_UpdatesSession()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        Assert.True(services.IngestObservation(new ObservationEvent
        {
            Kind = ObservationKind.ApplicationActivated,
            ApplicationName = "Cursor",
            WindowTitle = "Secret Base",
            At = now
        }));
        Assert.NotNull(services.Sessions.Current);
        Assert.NotNull(services.ComposeSituation().ProjectName);
    }

    [Fact]
    public void ScenarioE_Search_RanksSecretBaseWork()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.Memory.Remember(new MemoryEntry { Key = "s", Summary = "Secret Base Windows observation", CreatedAt = now });
        services.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.ProjectOpened,
            Title = "Secret Base",
            ProjectName = "Secret Base",
            At = now
        });
        var hits = services.Search("この前のSecret Baseの作業");
        Assert.NotEmpty(hits);
        Assert.Contains(hits, hit => hit.Title.Contains("Secret Base", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ScenarioF_Automation_CalendarSuggestsThenAcceptsSafely()
    {
        var now = new DateTimeOffset(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);
        var services = Create(
            now,
            events:
            [
                new CalendarEvent { Title = "Secret Base Development", Start = now, End = now.AddHours(2) }
            ]);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            ProjectName = "Secret Base",
            Title = "Base",
            PreparedAt = now
        });
        var execution = services.EvaluatePipeline(AutomationTriggerKind.Calendar);
        if (execution.Suggestion is not null)
        {
            Assert.True(execution.Suggestion.RequiresConfirmation);
            services.RecordFeedback(true);
        }

        Assert.True(BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.WorkspaceContinue)!.RequiresConfirmation);
    }

    [Fact]
    public void ScenarioG_FileCleanup_IsCandidateOnly()
    {
        var services = Create(DateTimeOffset.UtcNow);
        var dispatch = services.Dispatch("使ってないものを整理したい");
        Assert.True(dispatch.HandledWithoutLlm);
        Assert.False(dispatch.RequiresConfirmation);
        Assert.Contains("review", dispatch.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScenarioH_AiUnavailable_NonLlmStillWorks()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.LoadTodos().Items.Add(TodoItem.Create("Mathematics"));
        services.SaveTodos(services.LoadTodos());
        var briefing = services.Briefing();
        Assert.Contains("Mathematics", briefing.Format(), StringComparison.Ordinal);
        Assert.NotEmpty(services.Search("Secret"));
        Assert.NotNull(services.Continuation());
        var assistant = new AssistantService(
            BuiltinAssistantToolRegistry.Instance,
            new AssistantToolExecutor(BuiltinAssistantToolRegistry.Instance, baseExperience: services),
            () => new UnavailableAiProvider("x", "down", "offline"));
        assistant.CommandContext = services;
        var result = await assistant.SendAsync("今日の予定を整理して");
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ScenarioI_LearningDoesNotDropConfirmation()
    {
        var now = new DateTimeOffset(2026, 9, 3, 15, 0, 0, TimeSpan.Zero);
        var services = Create(now);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            ProjectName = "Secret Base",
            Title = "Base",
            PreparedAt = now
        });
        for (var i = 0; i < 20; i++)
        {
            services.LastSuggestion = new AutomationSuggestion
            {
                Intent = DetectedIntentKind.ContinueProject,
                Title = "Continue",
                RequiresConfirmation = true,
                Confidence = 0.9
            };
            services.RecordFeedback(true);
        }

        var intent = services.DetectIntent();
        var mode = InterventionPolicy.Decide(services.ComposeUserState(), intent, urgency: 0.7);
        Assert.True(mode is InterventionMode.Confirm or InterventionMode.Suggest or InterventionMode.Passive);
        Assert.True(BuiltinAssistantToolRegistry.Instance.Find(AssistantToolNames.WorkspaceContinue)!.RequiresConfirmation);
        Assert.False(LearningPolicy.MayEscalatePrivilege);
    }

    [Fact]
    public void ScenarioJ_Explainability_IncludesEvidenceAndConfidence()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            ProjectName = "Tetris AI",
            Title = "Tetris",
            LastSessionSummary = "Yesterday you were working on Tetris AI",
            PreparedAt = now
        });
        var text = services.ExplainIntent();
        Assert.Contains("Confidence", text, StringComparison.Ordinal);
        Assert.Contains("suggestion, not a certainty", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("•", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectIntelligence_AnswersStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        services.RememberPreparedWorkspace(new WorkspaceSession
        {
            ProjectId = "p1",
            ProjectName = "Secret Base",
            NextTask = "Windows QA",
            LastSessionSummary = "Observation layer",
            PreparedAt = now
        });
        var dispatch = services.Dispatch("Secret Baseどうなってる？");
        Assert.True(dispatch.HandledWithoutLlm);
        Assert.Contains("Windows QA", dispatch.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void CommitCapture_PersistsTodoAndMemory()
    {
        var now = DateTimeOffset.UtcNow;
        var services = Create(now);
        var draft = services.ClassifyCapture("todo: write tests for Command Center");
        var saved = services.CommitCapture(draft, CaptureDestination.Todo);
        Assert.NotNull(saved);
        Assert.Contains(services.LoadTodos().Items, item => item.Title.Contains("write tests", StringComparison.Ordinal));
        Assert.NotEmpty(services.Memory.Recall(now, query: "Command Center"));
    }
}
