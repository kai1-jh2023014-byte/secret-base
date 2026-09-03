using SecretBase.Core.Classroom;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Widgets;

public static class WidgetCatalogGroups
{
    public const string Information = "Information";
    public const string Creative = "Creative";
    public const string Ai = "AI";
    public const string Apps = "Apps";
    public const string Base = "Base";
}

public enum WidgetCatalogKind
{
    Widget = 0,
    WebPreset = 1
}

/// <summary>Add Widget catalog entry. Classroom is a Web preset, not a new widget type.</summary>
public sealed class WidgetCatalogEntry
{
    public required string Id { get; init; }

    public required string Group { get; init; }

    public required string Label { get; init; }

    public WidgetCatalogKind Kind { get; init; } = WidgetCatalogKind.Widget;

    public string? WidgetType { get; init; }

    public string? WebPresetUrl { get; init; }
}

/// <summary>Grouped Add Widget list. Does not invent a Classroom Widget type.</summary>
public static class WidgetCatalog
{
    private static readonly IReadOnlyList<WidgetCatalogEntry> All =
    [
        new()
        {
            Id = "clock",
            Group = WidgetCatalogGroups.Information,
            Label = "Clock",
            WidgetType = WidgetTypes.Clock
        },
        new()
        {
            Id = "text",
            Group = WidgetCatalogGroups.Information,
            Label = "Text",
            WidgetType = WidgetTypes.Text
        },
        new()
        {
            Id = "web",
            Group = WidgetCatalogGroups.Information,
            Label = "Web",
            WidgetType = WidgetTypes.Web
        },
        new()
        {
            Id = "calendar",
            Group = WidgetCatalogGroups.Information,
            Label = "Calendar",
            WidgetType = WidgetTypes.Calendar
        },
        new()
        {
            Id = "classroom",
            Group = WidgetCatalogGroups.Information,
            Label = "Classroom",
            Kind = WidgetCatalogKind.WebPreset,
            WidgetType = WidgetTypes.Web,
            WebPresetUrl = ClassroomUrls.Official
        },
        new()
        {
            Id = "creative",
            Group = WidgetCatalogGroups.Creative,
            Label = "Creative Projects",
            WidgetType = WidgetTypes.Creative
        },
        new()
        {
            Id = "music",
            Group = WidgetCatalogGroups.Creative,
            Label = "Music",
            WidgetType = WidgetTypes.Music
        },
        new()
        {
            Id = "ai",
            Group = WidgetCatalogGroups.Ai,
            Label = "AI Workspace",
            WidgetType = WidgetTypes.Ai
        },
        new()
        {
            Id = "assistant",
            Group = WidgetCatalogGroups.Ai,
            Label = "Base AI",
            WidgetType = WidgetTypes.Assistant
        },
        new()
        {
            Id = "workspace",
            Group = WidgetCatalogGroups.Base,
            Label = "Workspace",
            WidgetType = WidgetTypes.Workspace
        },
        new()
        {
            Id = "apps",
            Group = WidgetCatalogGroups.Apps,
            Label = "My Apps",
            WidgetType = WidgetTypes.Apps
        }
    ];

    public static IReadOnlyList<WidgetCatalogEntry> Entries => All;

    public static WidgetCatalogEntry? FindById(string? id) =>
        All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool TryResolveWebPresetUrl(WidgetCatalogEntry entry, out string url, out string error)
    {
        url = string.Empty;
        error = string.Empty;
        if (entry.Kind != WidgetCatalogKind.WebPreset)
        {
            error = "Not a web preset.";
            return false;
        }

        if (!WebUrlValidator.TryNormalize(entry.WebPresetUrl, out var normalized, out var urlError)
            || normalized is null)
        {
            error = urlError ?? WebUrlValidator.BlockedMessage;
            return false;
        }

        url = normalized;
        return true;
    }
}
