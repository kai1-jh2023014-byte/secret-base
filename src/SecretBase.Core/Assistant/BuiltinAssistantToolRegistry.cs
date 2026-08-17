using SecretBase.Core.Security;

namespace SecretBase.Core.Assistant;

/// <summary>Static MVP tool list. LLM may only call these names.</summary>
public sealed class BuiltinAssistantToolRegistry : IAiToolRegistry
{
    private static readonly IReadOnlyList<AssistantToolDefinition> All =
    [
        new()
        {
            Name = AssistantToolNames.CalendarGetToday,
            Description = "Read today's Secret Base calendar agenda.",
            RiskLevel = ActionPrivilege.Observation
        },
        new()
        {
            Name = AssistantToolNames.CalendarGetUpcoming,
            Description = "Read upcoming Secret Base calendar events for the next few days.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "days",
                    Type = "integer",
                    Description = "How many days ahead, 1-14. Default 7.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.Observation
        },
        new()
        {
            Name = AssistantToolNames.CreativeListProjects,
            Description = "List Creative Projects registered in Secret Base.",
            RiskLevel = ActionPrivilege.Observation
        },
        new()
        {
            Name = AssistantToolNames.CreativeOpenProject,
            Description = "Open a registered Creative Project dashboard by project_id from list_projects.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "project_id",
                    Type = "string",
                    Description = "Registered Creative Project id.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            RequiresConfirmation = true
        },
        new()
        {
            Name = AssistantToolNames.CursorOpenProject,
            Description = "Open a registered Creative Project folder in Cursor (not a shell command).",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "project_id",
                    Type = "string",
                    Description = "Registered Creative Project id.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.UserConfirmationRequired,
            RequiresConfirmation = true
        },
        new()
        {
            Name = AssistantToolNames.IntegrationOpen,
            Description = "Open a registered integration (classroom or calendar) in the browser / Web Widget.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "target",
                    Type = "string",
                    Description = "classroom or calendar",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            RequiresConfirmation = true
        },
        new()
        {
            Name = AssistantToolNames.AppsList,
            Description = "List My Apps registered in Secret Base.",
            RiskLevel = ActionPrivilege.Observation
        },
        new()
        {
            Name = AssistantToolNames.AppsOpen,
            Description = "Launch a registered My App by app_id from apps_list. Never runs free-form commands.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "app_id",
                    Type = "string",
                    Description = "Registered My Apps id.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            RequiresConfirmation = true
        },
        new()
        {
            Name = AssistantToolNames.MusicSearch,
            Description = "Search the Secret Base music catalog. Demo catalog only unless a real provider is wired.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "query",
                    Type = "string",
                    Description = "Track or artist search text. Not a URL or file path.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.Observation
        },
        new()
        {
            Name = AssistantToolNames.MusicPlay,
            Description = "Play a track_id returned by music_search when playback capability exists. Do not invent Spotify/YouTube playback.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "track_id",
                    Type = "string",
                    Description = "Track id from the previous search result.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            RequiresConfirmation = true
        }
    ];

    public IReadOnlyList<AssistantToolDefinition> Tools => All;

    public AssistantToolDefinition? Find(string? name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static BuiltinAssistantToolRegistry Instance { get; } = new();
}
