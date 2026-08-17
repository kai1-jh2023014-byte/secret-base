namespace SecretBase.Core.Integration;

/// <summary>Stable command ids for the Integration Catalog (future AI tool names).</summary>
public static class IntegrationCommandIds
{
    public const string CalendarGetTodayEvents = "calendar.getTodayEvents";
    public const string CalendarRefresh = "calendar.refresh";
    public const string CalendarOpen = "calendar.open";

    public const string MusicSearch = "music.search";
    public const string MusicPlay = "music.play";
    public const string MusicPause = "music.pause";
    public const string MusicNext = "music.next";

    public const string ClassroomGetAssignments = "classroom.getAssignments";
    public const string ClassroomRefresh = "classroom.refresh";
    public const string ClassroomOpen = "classroom.open";

    public const string CreativeListProjects = "creative.listProjects";
    public const string CreativeOpenProject = "creative.openProject";

    public const string AppsListApps = "apps.listApps";
    public const string AppsOpenApp = "apps.openApp";

    public const string CursorOpenProject = "cursor.openProject";
}

public static class IntegrationDomains
{
    public const string Calendar = "calendar";
    public const string Music = "music";
    public const string Classroom = "classroom";
    public const string Creative = "creative";
    public const string Apps = "apps";
    public const string Cursor = "cursor";
}
