namespace SecretBase.Core.Assistant;

/// <summary>Built-in LLM provider ids. Implementations live in Infrastructure.</summary>
public static class AssistantProviderIds
{
    public const string OpenAi = "openai";
    public const string Gemini = "gemini";
    public const string Local = "local";
}

/// <summary>OS secret-store key (Credential Manager / Keychain; implementations add their own prefix).</summary>
public static class AssistantSecretKeys
{
    public const string OpenAiApiKey = "Assistant/OpenAI/ApiKey";

    public const string GeminiApiKey = "Assistant/Gemini/ApiKey";
}

public static class AssistantToolNames
{
    public const string AssistantGetContext = "assistant_get_context";
    public const string CalendarGetToday = "calendar_get_today";
    public const string CalendarGetUpcoming = "calendar_get_upcoming";
    public const string CalendarAddEvent = "calendar_add_event";
    public const string CalendarRememberUsual = "calendar_remember_usual";
    public const string CalendarApplyUsual = "calendar_apply_usual";
    public const string CreativeListProjects = "creative_list_projects";
    public const string CreativeGetProject = "creative_get_project";
    public const string CreativeOpenProject = "creative_open_project";
    public const string CursorOpenProject = "cursor_open_project";
    public const string IntegrationOpen = "integration_open";
    public const string AppsList = "apps_list";
    public const string AppsOpen = "apps_open";
    public const string MusicSearch = "music_search";
    public const string MusicGetState = "music_get_state";
    public const string MusicPlay = "music_play";
    public const string ProjectRecommend = "project_recommend";
    public const string ScheduleRecommend = "schedule_recommend";
    public const string MusicRecommend = "music_recommend";
    public const string WorkspaceOpenNamed = "workspace_open_named";
    public const string WorkspaceRemove = "workspace_remove";
    public const string FilesDelete = "files_delete";
    public const string WorkspacePrepare = "workspace_prepare";
    public const string WorkspaceContinue = "workspace_continue";
    public const string TodoList = "todo_list";
    public const string TodoAdd = "todo_add";
    public const string FocusStart = "focus_start";
    public const string FilesSuggestCleanup = "files_suggest_cleanup";
    public const string MemoryRecall = "memory_recall";
    public const string MemoryRemember = "memory_remember";
    public const string ActivityRecent = "activity_recent";
    public const string SearchBase = "search_base";
    public const string UserState = "user_state";
    public const string AutomationFeedback = "automation_feedback";
    public const string SituationNow = "situation_now";
    public const string SessionRecent = "session_recent";
    public const string DailyBriefing = "daily_briefing";
    public const string CommandPalette = "command_palette";
    public const string QuickCapture = "quick_capture";
    public const string IntentExplain = "intent_explain";
    public const string ActivityTimeline = "activity_timeline";
    public const string PrivacyManifest = "privacy_manifest";
    public const string AttentionNow = "attention_now";
    public const string IntegrationsList = "integrations_list";
    public const string IntegrationQuery = "integration_query";
    public const string IntegrationInvoke = "integration_invoke";
}

public static class AssistantActivityDomains
{
    public const string Context = "Context";
    public const string Calendar = "Calendar";
    public const string Projects = "Projects";
    public const string Cursor = "Cursor";
    public const string Apps = "Apps";
    public const string Music = "Music";
    public const string Integration = "Integration";
    public const string Workspace = "Workspace";
    public const string Suggest = "Suggest";
    public const string Todo = "Todo";
    public const string Focus = "Focus";
    public const string Files = "Files";
    public const string Memory = "Memory";
    public const string Activity = "Activity";
    public const string Search = "Search";
    public const string State = "State";
    public const string Situation = "Situation";
    public const string Session = "Session";
    public const string Automation = "Automation";
}

public static class AssistantUserMessages
{
    public const string NotConfigured = "OpenAI API Key is not configured.";
    public const string OpenSettings = "Open AI Settings.";
    public const string Unavailable = "AI provider is unavailable.";
    public const string Timeout = "AI response timed out.";
    public const string NetworkError = "Could not connect to AI provider.";
    public const string AuthenticationFailed = "AI authentication failed.";
    public const string RateLimitReached = "AI provider rate limit reached.";
    public const string ToolUnavailable = "This action is currently unavailable.";
    public const string CalendarFailed = "Could not load Calendar.";
    public const string ProjectsFailed = "Could not load Projects.";
    public const string AppsFailed = "Could not load Apps.";
    public const string MusicFailed = "Could not load Music.";
    public const string IntegrationFailed = "Could not load Integrations.";
    public const string CursorOpenFailed = "Cursor could not be opened.";
    public const string CursorOpenSucceeded = "Opened the project in Cursor.";
    public const string ActionCancelled = "Cancelled.";
    public const string MaxStepsReached = "Stopped after the maximum number of steps.";
    public const string PendingConfirmationMustResolve =
        "Resolve the pending confirmation with Run or Cancel before sending a new message.";
    public const string DiskDeleteRefused =
        "Secret Base will not delete files on disk. Confirm to return a Block item to Desktop or unregister a Secret Base item.";
}
