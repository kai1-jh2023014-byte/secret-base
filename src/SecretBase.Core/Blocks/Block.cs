using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Blocks;

/// <summary>
/// A small "room" on the Desktop — movable/resizable host for launchable items.
/// Not a Windows folder substitute; first-class layout entity alongside widgets.
/// </summary>
public sealed class Block
{
    public const double DefaultWidth = 360;
    public const double DefaultHeight = 260;
    public const double MinWidth = 240;
    public const double MinHeight = 160;

    /// <summary>Narrow min size for fashion icon rails.</summary>
    public const double RailMinWidth = 72;
    public const double RailMinHeight = 140;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Block";

    public WidgetPosition Position { get; set; } = new(420, 48);

    public WidgetSize Size { get; set; } = new(DefaultWidth, DefaultHeight);

    /// <summary>
    /// Optional theme key. Null/empty = use the room <c>ThemeDefinition</c>.
    /// V1 stores the key only; visuals always apply the active room theme.
    /// </summary>
    public string? Theme { get; set; }

    /// <summary><see cref="BlockIconStyle"/> — color or white silhouette.</summary>
    public string IconStyle { get; set; } = BlockIconStyle.Color;

    /// <summary><see cref="BlockLayoutMode"/> — grid or compact rail.</summary>
    public string LayoutMode { get; set; } = BlockLayoutMode.Grid;

    /// <summary>
    /// When false, grid tiles hide the name under each icon (rail always hides labels).
    /// </summary>
    public bool ShowLabels { get; set; } = true;

    public RoomId RoomId { get; set; } = RoomId.DefaultRoomId;

    public List<BlockItem> Items { get; set; } = [];

    public void ClampSize()
    {
        IconStyle = BlockIconStyle.Normalize(IconStyle);
        LayoutMode = BlockLayoutMode.Normalize(LayoutMode);
        if (BlockLayoutMode.IsRail(LayoutMode))
        {
            Size.Clamp(RailMinWidth, RailMinHeight);
            return;
        }

        Size.Clamp(MinWidth, MinHeight);
    }
}
