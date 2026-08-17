using SecretBase.Core.Security;

namespace SecretBase.Core.Integration;

/// <summary>
/// Static list of operations Secret Base already knows. Future AI selects from this catalog
/// and emits Commands — it does not gain Shell or OS APIs.
/// </summary>
public static class IntegrationCatalog
{
    private static readonly IReadOnlyList<IntegrationCommandDescriptor> All =
    [
        new()
        {
            Id = IntegrationCommandIds.CalendarGetTodayEvents,
            Domain = IntegrationDomains.Calendar,
            Name = "GetTodayEvents",
            Description = "Read today's agenda from configured calendar providers.",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.CalendarRefresh,
            Domain = IntegrationDomains.Calendar,
            Name = "Refresh",
            Description = "Refresh today's agenda.",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.CalendarOpen,
            Domain = IntegrationDomains.Calendar,
            Name = "Open",
            Description = "Open the calendar URL in the system browser.",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.MusicSearch,
            Domain = IntegrationDomains.Music,
            Name = "Search",
            Description = "Search the music catalog (query required).",
            Arguments = "query",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.MusicPlay,
            Domain = IntegrationDomains.Music,
            Name = "Play",
            Description = "Play a previously searched track.",
            Arguments = "track (via MusicCommand)",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.MusicPause,
            Domain = IntegrationDomains.Music,
            Name = "Pause",
            Description = "Pause current playback.",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.MusicNext,
            Domain = IntegrationDomains.Music,
            Name = "Next",
            Description = "Skip to the next track when the provider supports it.",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.ClassroomGetAssignments,
            Domain = IntegrationDomains.Classroom,
            Name = "GetAssignments",
            Description = "Read Classroom assignments when a real provider exists (currently unavailable).",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.ClassroomRefresh,
            Domain = IntegrationDomains.Classroom,
            Name = "Refresh",
            Description = "Open official Google Classroom (no Classroom API in this repo).",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.ClassroomOpen,
            Domain = IntegrationDomains.Classroom,
            Name = "Open",
            Description = "Open official Google Classroom via the existing Web Widget / browser.",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.CreativeListProjects,
            Domain = IntegrationDomains.Creative,
            Name = "ListProjects",
            Description = "List registered Creative Projects.",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.CreativeOpenProject,
            Domain = IntegrationDomains.Creative,
            Name = "OpenProject",
            Description = "Open a registered Creative Project dashboard (no auto-launch).",
            Arguments = "projectId",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.AppsListApps,
            Domain = IntegrationDomains.Apps,
            Name = "ListApps",
            Description = "List registered My Apps.",
            Privilege = ActionPrivilege.Observation,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.AppsOpenApp,
            Domain = IntegrationDomains.Apps,
            Name = "OpenApp",
            Description = "Launch a registered custom app (validated target only).",
            Arguments = "appId",
            Privilege = ActionPrivilege.SafeAction,
            Trust = TrustBoundary.BuiltInWidget
        },
        new()
        {
            Id = IntegrationCommandIds.CursorOpenProject,
            Domain = IntegrationDomains.Cursor,
            Name = "OpenProject",
            Description = "Open a registered Creative Project root in Cursor.",
            Arguments = "projectId",
            Privilege = ActionPrivilege.UserConfirmationRequired,
            Trust = TrustBoundary.AiAgent
        }
    ];

    public static IReadOnlyList<IntegrationCommandDescriptor> Commands => All;

    public static IntegrationCommandDescriptor? FindById(string? id) =>
        All.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
