using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Attention;
using SecretBase.Core.Automation;
using SecretBase.Core.Briefing;
using SecretBase.Core.Calendar;
using SecretBase.Core.Capture;
using SecretBase.Core.Commands;
using SecretBase.Core.Creative;
using SecretBase.Core.Explain;
using SecretBase.Core.Files;
using SecretBase.Core.Focus;
using SecretBase.Core.Intent;
using SecretBase.Core.Memory;
using SecretBase.Core.Observation;
using SecretBase.Core.Privacy;
using SecretBase.Core.Projects;
using SecretBase.Core.Search;
using SecretBase.Core.Session;
using SecretBase.Core.Situation;
using SecretBase.Core.State;
using SecretBase.Core.Timeline;
using SecretBase.Core.Todo;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Base;

/// <summary>
/// Composition of Base domains for Assistant tools. Not a widget state bag.
/// </summary>
public interface IBaseExperienceServices
{
    TodoList LoadTodos();

    void SaveTodos(TodoList list);

    FocusSessionStore Focus { get; }

    WorkspaceSession? CurrentWorkspace { get; set; }

    IMemoryStore Memory { get; }

    IActivityLog Activity { get; }

    IAutomationFeedbackStore AutomationFeedback { get; }

    IWorkSessionStore Sessions { get; }

    AutomationSuggestion? LastSuggestion { get; set; }

    DateTimeOffset? LastInterventionAt { get; }

    IReadOnlyList<CreativeProject> ListProjects();

    IReadOnlyList<CustomApp> ListApps();

    IReadOnlyList<CalendarEvent> ListUpcomingEvents();

    DateTimeOffset Now { get; }

    UserState ComposeUserState(
        AssistantMusicState? music = null,
        AssistantProviderStatusInfo? provider = null);

    DetectedIntent DetectIntent(string? utterance = null);

    CurrentSituation ComposeSituation();

    ProjectContinuationContext Continuation();

    AutomationSuggestion? EvaluateAutomation(AutomationTriggerKind trigger = AutomationTriggerKind.Time);

    AutomationExecution EvaluatePipeline(AutomationTriggerKind trigger = AutomationTriggerKind.Time);

    IReadOnlyList<SearchHit> Search(string query);

    bool IngestObservation(ObservationEvent observation);

    UserModelSnapshot UserModel();

    void RememberPreparedWorkspace(WorkspaceSession session);

    void RecordFeedback(bool accepted);

    BaseSettings? Preferences { get; set; }

    IAutomationRuleStore Rules { get; }

    DailyBriefingSnapshot Briefing();

    IReadOnlyList<PaletteItem> Palette(string query);

    CommandDispatch Dispatch(string utterance);

    IReadOnlyList<TimelineEntry> Timeline();

    CaptureDraft ClassifyCapture(string text);

    MemoryEntry? CommitCapture(CaptureDraft draft, CaptureDestination? force = null);

    string ExplainIntent();

    string Privacy();

    IReadOnlyList<AttentionItem> Attention();

    IReadOnlyList<SuggestionAudit> Audits();

    ProjectIntelligenceSnapshot? ProjectInfo(string name);

    IReadOnlyList<LearningInsight> Learning();

    void RecordOpened();

    string MemoryCatalog(string? query = null);
}

public sealed class BaseExperienceServices : IBaseExperienceServices, ICommandContext
{
    private readonly ITodoStore _todos;
    private readonly Func<IReadOnlyList<CreativeProject>> _projects;
    private readonly Func<IReadOnlyList<CustomApp>> _apps;
    private readonly Func<IReadOnlyList<CalendarEvent>> _events;
    private readonly Func<DateTimeOffset> _now;

    public BaseExperienceServices(
        ITodoStore todos,
        FocusSessionStore focus,
        Func<IReadOnlyList<CreativeProject>> projects,
        Func<IReadOnlyList<CustomApp>> apps,
        Func<IReadOnlyList<CalendarEvent>> events,
        Func<DateTimeOffset> now,
        IMemoryStore? memory = null,
        IActivityLog? activity = null,
        IAutomationFeedbackStore? feedback = null,
        IWorkSessionStore? sessions = null,
        IAutomationRuleStore? rules = null)
    {
        _todos = todos ?? throw new ArgumentNullException(nameof(todos));
        Focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _apps = apps ?? throw new ArgumentNullException(nameof(apps));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        Memory = memory ?? new MemoryStore();
        Activity = activity ?? new ActivityLog();
        AutomationFeedback = feedback ?? new AutomationFeedbackStore();
        Sessions = sessions ?? new WorkSessionStore();
        Rules = rules ?? new AutomationRuleStore();
    }

    public FocusSessionStore Focus { get; }

    public WorkspaceSession? CurrentWorkspace { get; set; }

    public IMemoryStore Memory { get; }

    public IActivityLog Activity { get; }

    public IAutomationFeedbackStore AutomationFeedback { get; }

    public IWorkSessionStore Sessions { get; }

    public IAutomationRuleStore Rules { get; }

    public BaseSettings? Preferences { get; set; }

    public AutomationSuggestion? LastSuggestion { get; set; }

    public DateTimeOffset? LastInterventionAt { get; private set; }

    private bool _focusWasRunning;
    private readonly List<SuggestionAudit> _audits = [];

    public TodoList LoadTodos() => _todos.LoadOrCreate();

    public void SaveTodos(TodoList list) => _todos.Save(list);

    public IReadOnlyList<CreativeProject> ListProjects() => _projects();

    public IReadOnlyList<CustomApp> ListApps() => _apps();

    public IReadOnlyList<CalendarEvent> ListUpcomingEvents() => _events();

    public DateTimeOffset Now => _now();

    public int DefaultFocusMinutes => Math.Clamp(Preferences?.DefaultFocusMinutes ?? 25, 5, 90);

    public UserState ComposeUserState(
        AssistantMusicState? music = null,
        AssistantProviderStatusInfo? provider = null) =>
        UserStateComposer.Compose(
            Now,
            CurrentWorkspace,
            Focus.Current,
            ListUpcomingEvents(),
            LoadTodos(),
            Activity.Meaningful(Now),
            music,
            provider,
            Memory.Recall(Now));

    public DetectedIntent DetectIntent(string? utterance = null) =>
        IntentEngine.Detect(ComposeUserState(), ComposeSituation(), Sessions.Current ?? Sessions.Recent(1).FirstOrDefault(), utterance);

    public CurrentSituation ComposeSituation()
    {
        var state = ComposeUserState();
        return SituationComposer.Compose(
            state,
            Activity.Meaningful(Now),
            Memory.RecallRanked(Now, projectName: state.CurrentProjectName),
            Sessions.Current ?? Sessions.Recent(1).FirstOrDefault(),
            Focus.Current,
            ListUpcomingEvents(),
            LoadTodos());
    }

    public ProjectContinuationContext Continuation() =>
        ProjectContinuation.Build(
            ComposeUserState(),
            CurrentWorkspace,
            Memory.RecallRanked(Now, projectName: ComposeUserState().CurrentProjectName),
            Activity.Meaningful(Now),
            LoadTodos(),
            Sessions.Current ?? Sessions.Recent(1).FirstOrDefault());

    public AutomationSuggestion? EvaluateAutomation(AutomationTriggerKind trigger = AutomationTriggerKind.Time) =>
        EvaluatePipeline(trigger).Suggestion;

    public AutomationExecution EvaluatePipeline(AutomationTriggerKind trigger = AutomationTriggerKind.Time)
    {
        var running = Focus.Current is { IsRunning: true } && !Focus.Current.IsComplete(Now);
        if (_focusWasRunning && !running)
        {
            trigger = AutomationTriggerKind.FocusEnded;
            Activity.Record(new ActivityEvent
            {
                Kind = ActivityKind.FocusEnded,
                Title = "Focus ended",
                ProjectName = CurrentWorkspace?.ProjectName,
                At = Now
            });
            Sessions.Touch("focus ended", null, null, false);
        }

        _focusWasRunning = running;
        var state = ComposeUserState();
        var situation = ComposeSituation();
        var intent = IntentEngine.Detect(state, situation, Sessions.Current ?? Sessions.Recent(1).FirstOrDefault());
        situation.LikelyIntent = intent.Kind.ToString();
        situation.NextLikelyAction = intent.IsActionable ? "Continue with confirmation" : "Stay quiet";
        var execution = AutomationEngine.Run(
            state,
            intent,
            AutomationFeedback,
            trigger,
            LastInterventionAt,
            Preferences?.QuietHoursStart ?? 22,
            Preferences?.QuietHoursEnd ?? 8,
            Preferences?.AllowFocusInterruptions ?? false);
        var matched = AutomationScheduler.Match(Rules.List(), trigger, intent, Now);
        if (matched is not null)
        {
            matched.LastRun = Now;
            matched.LastResult = execution.Suggestion?.Title ?? execution.Result ?? execution.Mode.ToString();
            Rules.Save(matched);
        }

        if (execution.Suggestion is not null)
        {
            _audits.Add(new SuggestionAudit
            {
                At = Now,
                What = execution.Suggestion.Title,
                Why = string.Join("; ", execution.Evidence.Take(4)),
                Confidence = execution.Suggestion.Confidence,
                Action = execution.Suggestion.RequiresConfirmation ? "Waiting for confirmation" : "Safe Auto suggestion",
                Status = "suggested",
                Evidence = execution.Evidence
            });
            if (_audits.Count > 80)
            {
                _audits.RemoveRange(0, _audits.Count - 80);
            }
        }

        LastSuggestion = execution.Suggestion;
        if (execution.Suggestion is not null)
        {
            LastInterventionAt = Now;
        }

        return execution;
    }

    public IReadOnlyList<SearchHit> Search(string query)
    {
        var suggestion = LastSuggestion;
        SearchHit[] extra = suggestion is null
            ? []
            : [new SearchHit("suggestion", suggestion.Title, suggestion.Detail, 0.7, Now, "automation")];
        return new KeywordSearchRanker().Rank(
            BaseSearch.Query(
                query,
                Memory.RecallRanked(Now, query, ComposeUserState().CurrentProjectName, 40),
                Activity.Recent(80),
                ListProjects(),
                ListUpcomingEvents(),
                LoadTodos(),
                CurrentWorkspace,
                Sessions.Recent(8),
                extra),
            query);
    }

    public DailyBriefingSnapshot Briefing() =>
        DailyBriefingComposer.Compose(
            ComposeUserState(),
            ComposeSituation(),
            DetectIntent(),
            Continuation(),
            ListUpcomingEvents(),
            LoadTodos(),
            Sessions.Current ?? Sessions.Recent(1).FirstOrDefault());

    public IReadOnlyList<PaletteItem> Palette(string query) =>
        CommandPalette.Build(
            query,
            ComposeUserState(),
            ComposeSituation(),
            DetectIntent(query),
            Search(string.IsNullOrWhiteSpace(query) ? "recent" : query),
            ListProjects(),
            ListApps(),
            LoadTodos());

    public CommandDispatch Dispatch(string utterance) => CommandCenter.Handle(utterance, this);

    public IReadOnlyList<TimelineEntry> Timeline() =>
        ActivityTimeline.ForDay(
            Now,
            Activity.Meaningful(Now, TimeSpan.FromHours(18)),
            ListUpcomingEvents(),
            Sessions.Recent(8),
            LoadTodos());

    public CaptureDraft ClassifyCapture(string text) =>
        QuickCaptureClassifier.Classify(text, ListProjects().Select(item => item.Name).ToList());

    public MemoryEntry? CommitCapture(CaptureDraft draft, CaptureDestination? force = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var destination = force ?? draft.Destination;
        if (destination == CaptureDestination.Todo)
        {
            var list = LoadTodos();
            list.Items.Add(QuickCaptureClassifier.ToTodo(draft));
            SaveTodos(list);
            Activity.Record(new ActivityEvent { Kind = ActivityKind.TodoCreated, Title = draft.Text, At = Now, Source = "quick-capture" });
            try
            {
                return Memory.Remember(QuickCaptureClassifier.ToMemory(draft, Now));
            }
            catch (InvalidOperationException)
            {
                return QuickCaptureClassifier.ToMemory(draft, Now);
            }
        }

        var memory = QuickCaptureClassifier.ToMemory(draft, Now);
        try
        {
            return Memory.Remember(memory);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public string ExplainIntent() => IntentExplainer.Explain(DetectIntent(), ComposeSituation());

    public string Privacy() => PrivacyManifest.Format();

    public IReadOnlyList<AttentionItem> Attention()
    {
        var state = ComposeUserState();
        var situation = ComposeSituation();
        var intent = DetectIntent();
        var mode = InterventionPolicy.Decide(
            state,
            intent,
            0.4,
            AutomationFeedback.ConsecutiveDismissals(intent.Kind),
            LastInterventionAt,
            Preferences?.QuietHoursStart ?? 22,
            Preferences?.QuietHoursEnd ?? 8,
            Preferences?.AllowFocusInterruptions ?? false);
        return AttentionCenter.Compose(
            state,
            situation,
            intent,
            LastSuggestion,
            LoadTodos(),
            ListUpcomingEvents(),
            FileIntelligence.SuggestCleanup(ListProjects(), Now),
            mode);
    }

    public IReadOnlyList<SuggestionAudit> Audits() => _audits.TakeLast(20).Reverse().ToList();

    public ProjectIntelligenceSnapshot? ProjectInfo(string name) =>
        ProjectIntelligence.For(
            name,
            ListProjects(),
            CurrentWorkspace,
            Sessions.Current ?? Sessions.Recent(1).FirstOrDefault(),
            Activity.Meaningful(Now),
            Memory.RecallRanked(Now, name, name, 8),
            LoadTodos());

    public string MemoryCatalog(string? query = null) =>
        PersonalSpaceCatalog.Memories(Memory, Now, query);

    public bool IngestObservation(ObservationEvent observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var last = Activity.Recent(1).LastOrDefault();
        var mapped = ObservationNormalizer.ToActivity(observation, ListApps(), ListProjects(), last);
        if (mapped is null)
        {
            return false;
        }

        Activity.Record(mapped);
        if (!string.IsNullOrWhiteSpace(mapped.ProjectName) || mapped.Kind == ActivityKind.ApplicationOpened)
        {
            Sessions.StartOrContinue(Now, CurrentWorkspace?.ProjectId, mapped.ProjectName ?? CurrentWorkspace?.ProjectName, CurrentWorkspace?.Title);
            Sessions.Touch(mapped.Title, mapped.Detail, LoadTodos().Items.FirstOrDefault(item => !item.IsDone)?.Title, false);
        }

        return true;
    }

    public UserModelSnapshot UserModel() =>
        UserModelBuilder.From(
            Activity.Recent(100),
            ListProjects(),
            ListApps(),
            preferredFocusMinutes: DefaultFocusMinutes,
            feedback: AutomationFeedback.Recent(40),
            preferredWorkspace: Preferences?.PreferredProjectName ?? CurrentWorkspace?.Title);

    public IReadOnlyList<LearningInsight> Learning() => LearningLoop.Detect(AutomationFeedback.Recent(80));

    public void RecordOpened()
    {
        Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.SecretBaseOpened,
            Title = "Secret Base opened",
            At = Now,
            Source = "host"
        });
        Sessions.StartOrContinue(Now, CurrentWorkspace?.ProjectId, CurrentWorkspace?.ProjectName, CurrentWorkspace?.Title);
    }

    public void RememberPreparedWorkspace(WorkspaceSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        CurrentWorkspace = session;
        Sessions.StartOrContinue(Now, session.ProjectId, session.ProjectName, session.Title);
        Sessions.Touch(session.LastSessionSummary, session.SuggestedFileNames.FirstOrDefault(), session.NextTask, false);
        Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.WorkspacePrepared,
            Title = "Workspace prepared",
            ProjectName = session.ProjectName,
            Detail = session.LastSessionSummary,
            At = Now
        });
        TryRemember(new MemoryEntry
        {
            Scope = MemoryScope.Session,
            Key = "last-session",
            Summary = string.IsNullOrWhiteSpace(session.LastSessionSummary) ? session.Title : session.LastSessionSummary,
            Detail = session.NextTask,
            ProjectId = session.ProjectId,
            ProjectName = session.ProjectName,
            Source = "workspace",
            Confidence = 0.8,
            Importance = MemoryImportance.High,
            CreatedAt = Now,
            LastAccessedAt = Now,
            ExpiresAt = MemoryPolicy.DefaultExpiry(MemoryScope.Session, Now)
        });
        if (!string.IsNullOrWhiteSpace(session.ProjectName))
        {
            TryRemember(new MemoryEntry
            {
                Scope = MemoryScope.Project,
                Key = "current-project",
                Summary = session.ProjectName,
                Detail = session.NextTask,
                ProjectId = session.ProjectId,
                ProjectName = session.ProjectName,
                Source = "workspace",
                Confidence = 0.78,
                Importance = MemoryImportance.High,
                CreatedAt = Now,
                LastAccessedAt = Now,
                ExpiresAt = MemoryPolicy.DefaultExpiry(MemoryScope.Project, Now)
            });
        }
    }

    public void RecordFeedback(bool accepted)
    {
        if (LastSuggestion is null)
        {
            return;
        }

        AutomationFeedback.Record(new AutomationFeedback
        {
            SuggestionId = LastSuggestion.Id,
            Intent = LastSuggestion.Intent,
            Accepted = accepted,
            At = Now,
            ProjectName = LastSuggestion.ProjectName
        });
        Activity.Record(new ActivityEvent
        {
            Kind = accepted ? ActivityKind.SuggestionAccepted : ActivityKind.SuggestionDismissed,
            Title = accepted ? "Suggestion accepted" : "Suggestion dismissed",
            ProjectName = LastSuggestion.ProjectName,
            At = Now
        });
        if (accepted)
        {
            Sessions.Touch("suggestion accepted", null, null, false);
        }
    }

    private void TryRemember(MemoryEntry entry)
    {
        try
        {
            Memory.Remember(entry);
        }
        catch (InvalidOperationException)
        {
            // Sensitive or path-like payloads are refused by MemoryPolicy.
        }
    }
}
