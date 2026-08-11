using System.Text.Json;
using SecretBase.Core.Desktop;

namespace SecretBase.Core.Widgets;

/// <summary>
/// A placed widget on a Desktop/Room. UI-agnostic — presentation lives in Widgets/App.
/// </summary>
public sealed class WidgetInstance
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Widget type key, e.g. <see cref="WidgetTypes.Clock"/>.</summary>
    public string Type { get; set; } = string.Empty;

    public WidgetPosition Position { get; set; } = new();

    public WidgetSize Size { get; set; } = new();

    public RoomId RoomId { get; set; } = RoomId.DefaultRoomId;

    /// <summary>
    /// Type-specific settings as JSON object properties.
    /// Keeps Core free of per-widget UI concerns while allowing future options.
    /// </summary>
    public Dictionary<string, JsonElement> Configuration { get; set; } = new(StringComparer.Ordinal);
}
