namespace SecretBase.Core.Assistant;

/// <summary>Built-in LLM provider ids. Implementations live in Infrastructure.</summary>
public static class AssistantProviderIds
{
    public const string OpenAi = "openai";
    public const string Gemini = "gemini";
    public const string Local = "local";
}

/// <summary>Credential Manager key (prefixed with SecretBase/ by the Windows store).</summary>
public static class AssistantSecretKeys
{
    public const string OpenAiApiKey = "Assistant/OpenAI/ApiKey";
}

public static class AssistantToolNames
{
    public const string CalendarGetToday = "calendar_get_today";
    public const string CalendarGetUpcoming = "calendar_get_upcoming";
    public const string CreativeListProjects = "creative_list_projects";
    public const string CreativeOpenProject = "creative_open_project";
    public const string CursorOpenProject = "cursor_open_project";
    public const string IntegrationOpen = "integration_open";
    public const string AppsList = "apps_list";
    public const string AppsOpen = "apps_open";
    public const string MusicSearch = "music_search";
    public const string MusicPlay = "music_play";
}

public static class AssistantUserMessages
{
    public const string NotConfigured = "AI is not configured.";
    public const string OpenSettings = "Open AI Settings.";
    public const string Unavailable = "AI service is unavailable. Please try again.";
    public const string ToolUnavailable = "This action is currently unavailable.";
    public const string CursorOpenFailed = "Cursor could not be opened.";
}
