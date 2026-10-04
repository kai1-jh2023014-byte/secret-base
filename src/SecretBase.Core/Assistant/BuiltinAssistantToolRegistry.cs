using SecretBase.Core.Security;

namespace SecretBase.Core.Assistant;

/// <summary>Static tool list. LLM may only call these names. HostAction tools are never listed.</summary>
public sealed class BuiltinAssistantToolRegistry : IAiToolRegistry
{
    private static readonly IReadOnlyList<AssistantToolDefinition> All =
    [
        new()
        {
            Name = AssistantToolNames.AssistantGetContext,
            Description =
                "Read a scoped Secret Base overview (calendar, free time, projects, apps, music, integrations, provider). Prefer this once per turn with needed domains. Read-only.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.CalendarGetToday,
            Description = "Read today's Secret Base calendar agenda.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
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
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.CalendarAddEvent,
            Description =
                "Add a local Secret Base calendar event for today (title + time). Shows in the Calendar widget. Does not write to Google. Requires confirmation.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "title",
                    Type = "string",
                    Description = "Event title. Not a file path.",
                    Required = true
                },
                new AssistantToolParameter
                {
                    Name = "hour",
                    Type = "integer",
                    Description = "Start hour 0-23. Default is the current hour.",
                    Required = false
                },
                new AssistantToolParameter
                {
                    Name = "minute",
                    Type = "integer",
                    Description = "Start minute 0-59. Default 0.",
                    Required = false
                },
                new AssistantToolParameter
                {
                    Name = "duration_minutes",
                    Type = "integer",
                    Description = "Length in minutes, 15-480. Default 60.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.CalendarRememberUsual,
            Description =
                "Remember a usual local schedule slot (title + time) for later apply. Requires confirmation.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "title",
                    Type = "string",
                    Description = "Usual event title.",
                    Required = true
                },
                new AssistantToolParameter
                {
                    Name = "hour",
                    Type = "integer",
                    Description = "Start hour 0-23.",
                    Required = true
                },
                new AssistantToolParameter
                {
                    Name = "minute",
                    Type = "integer",
                    Description = "Start minute 0-59. Default 0.",
                    Required = false
                },
                new AssistantToolParameter
                {
                    Name = "duration_minutes",
                    Type = "integer",
                    Description = "Length in minutes. Default 60.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.CalendarApplyUsual,
            Description =
                "Apply the remembered usual schedule onto today's local calendar. Fails honestly if none is saved. Requires confirmation.",
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.CreativeListProjects,
            Description = "List Creative Projects registered in Secret Base.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.CreativeGetProject,
            Description =
                "Read one Creative Project by project_id (name, description, notes preview, quick actions). Read-only; does not open or launch.",
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
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.ProjectRecommend,
            Description =
                "Suggest which registered Creative Project to work on next based on favorites/recent/notes. Suggestion only — does not open Cursor or projects.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.Suggest
        },
        new()
        {
            Name = AssistantToolNames.ScheduleRecommend,
            Description =
                "Suggest a day priority from today's calendar + registered projects. Phrase as candidates, never as certainty about the user's life.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.Suggest
        },
        new()
        {
            Name = AssistantToolNames.MusicRecommend,
            Description =
                "Suggest a track from the Secret Base music catalog (demo catalog honesty). Suggestion only — does not play.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "query",
                    Type = "string",
                    Description = "Optional mood or artist hint.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.Suggest
        },
        new()
        {
            Name = AssistantToolNames.CreativeOpenProject,
            Description =
                "Open a registered Creative Project dashboard by project_id. Requires user confirmation. Suggest first if the user did not ask to open.",
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
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.CursorOpenProject,
            Description =
                "Open a registered Creative Project folder in Cursor (not a shell command). Requires user confirmation. Suggest first if the user did not ask to open.",
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
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.IntegrationOpen,
            Description =
                "Open a registered integration (classroom or calendar) in the browser / Web Widget. Requires confirmation.",
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
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.AppsList,
            Description = "List My Apps registered in Secret Base.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.AppsOpen,
            Description =
                "Launch a registered My App by app_id from apps_list. Never runs free-form commands. Requires confirmation.",
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
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.MusicSearch,
            Description =
                "Search music. If Spotify's catalog API refuses the account, this opens the Spotify search page for the query.",
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
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.MusicGetState,
            Description =
                "Read current Secret Base music state (capabilities, demo catalog note, current track if any). Read-only.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.MusicPlay,
            Description =
                "Play or change the current song. Pass query (song or artist) or track_id from music_search. If Spotify's catalog API cannot play it, this opens the Spotify search page. Requires confirmation.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "track_id",
                    Type = "string",
                    Description = "Track id from music_search. Optional if query is set.",
                    Required = false
                },
                new AssistantToolParameter
                {
                    Name = "query",
                    Type = "string",
                    Description = "Song or artist to search then play. Not a URL or file path.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.WorkspaceOpenNamed,
            Description =
                "Open a registered Block item, My App, or Creative Project by display name (not a file path). Requires confirmation.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "name",
                    Type = "string",
                    Description = "Registered name as shown in Secret Base. Not a disk path.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.WorkspaceRemove,
            Description =
                "After confirmation: return a hidden Block item to Desktop, or unregister a My App / Creative Project / local event. NEVER deletes files on disk.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "name",
                    Type = "string",
                    Description = "Registered Secret Base name. Paths are rejected.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.UserConfirmationRequired,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.FilesDelete,
            Description =
                "Important: Secret Base never deletes OS files. Same as workspace_remove — return a Block item to Desktop or unregister a Secret Base item after confirmation. If the user asked to delete a disk file that is not registered, refuse.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "name",
                    Type = "string",
                    Description = "Registered name only. File paths are rejected.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.UserConfirmationRequired,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.WorkspacePrepare,
            Description =
                "Prepare a Secret Base workspace from registered projects, apps, todos, and calendar. Display only — does not launch apps, open files, or run git. Safe Auto.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "intent",
                    Type = "string",
                    Description = "What the user wants to continue, e.g. Secret Base development. Not a file path.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.SafeAuto
        },
        new()
        {
            Name = AssistantToolNames.WorkspaceContinue,
            Description =
                "After confirmation: open the prepared workspace's registered Creative Project. Does not run git or launch arbitrary executables.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "intent",
                    Type = "string",
                    Description = "Optional intent if a workspace is not already prepared.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.TodoList,
            Description = "List local Secret Base todos. Read-only. Not Google Tasks.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.ReadOnly
        },
        new()
        {
            Name = AssistantToolNames.TodoAdd,
            Description = "Add a local Secret Base todo. Requires confirmation. Does not write to Google.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "title",
                    Type = "string",
                    Description = "Todo title. Not a file path.",
                    Required = true
                }
            ],
            RiskLevel = ActionPrivilege.SafeAction,
            Capability = AssistantToolCapability.RequiresConfirmation
        },
        new()
        {
            Name = AssistantToolNames.FocusStart,
            Description =
                "Open the Pomodoro widget and start (or show) a local focus timer. "
                + "Use when the user wants a Pomodoro / focus timer now (ポモドーロ・集中タイマー・pomodoro). "
                + "Safe Auto — does not launch apps. If a timer is already running, shows it instead of stacking.",
            Parameters =
            [
                new AssistantToolParameter
                {
                    Name = "minutes",
                    Type = "integer",
                    Description = "Focus duration 5-90. Default 25. Ignored when a session is already running.",
                    Required = false
                },
                new AssistantToolParameter
                {
                    Name = "label",
                    Type = "string",
                    Description = "Optional label such as Pomodoro.",
                    Required = false
                }
            ],
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.SafeAuto
        },
        new()
        {
            Name = AssistantToolNames.FilesSuggestCleanup,
            Description =
                "Suggest unused registered files for review. Never deletes. Suggestion only.",
            RiskLevel = ActionPrivilege.Observation,
            Capability = AssistantToolCapability.Suggest
        }
    ];

    public IReadOnlyList<AssistantToolDefinition> Tools => All;

    public AssistantToolDefinition? Find(string? name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    public static BuiltinAssistantToolRegistry Instance { get; } = new();
}
