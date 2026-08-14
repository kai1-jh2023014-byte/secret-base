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
}
