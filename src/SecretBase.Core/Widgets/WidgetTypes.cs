namespace SecretBase.Core.Widgets;

/// <summary>
/// Built-in widget type identifiers. Third-party/plugin types will use a separate namespace later.
/// </summary>
public static class WidgetTypes
{
    public const string Clock = "clock";
    public const string Text = "text";

    /// <summary>WebView2-hosted page. Untrusted content — no host bridge.</summary>
    public const string Web = "web";

    /// <summary>Local month calendar. Events via <c>ICalendarEventSource</c> (local-only in v0.1).</summary>
    public const string Calendar = "calendar";

    /// <summary>Music Hub widget — music sources (Spotify/YouTube web, Local placeholder). Untrusted WebView when browsing.</summary>
    public const string Music = "music";

    /// <summary>Creative Workspace — user-registered projects/files/folders (open only).</summary>
    public const string Creative = "creative";

    /// <summary>AI Workspace — launch Cursor / official AI websites with Project context.</summary>
    public const string Ai = "ai";

    /// <summary>My Apps — registered custom apps (launch only; not a plugin host).</summary>
    public const string Apps = "apps";

    /// <summary>Secret Base AI chat — tools over existing Commands. Not the Cursor/ChatGPT launcher.</summary>
    public const string Assistant = "assistant";

    /// <summary>Prepared work mode: project, registered apps/files, next task, focus.</summary>
    public const string Workspace = "workspace";
}
