using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Desktop session state for a Room. Widget instances are UI-agnostic.
/// </summary>
public sealed class DesktopLayout
{
    public RoomId RoomId { get; set; } = RoomId.DefaultRoomId;

    /// <summary>Schema version for layout JSON migrations.</summary>
    public int SchemaVersion { get; set; } = 1;

    public List<WidgetInstance> Widgets { get; set; } = [];

    public static DesktopLayout CreateDefault()
    {
        return new DesktopLayout
        {
            RoomId = RoomId.DefaultRoomId,
            SchemaVersion = 1,
            // First-run seed (no Add Widget UI yet): Clock + Text.
            Widgets =
            [
                DefaultWidgetFactory.CreateDefaultClock(),
                DefaultWidgetFactory.CreateDefaultText()
            ]
        };
    }
}
