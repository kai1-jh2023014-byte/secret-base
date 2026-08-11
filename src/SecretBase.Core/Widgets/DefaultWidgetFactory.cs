using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;

namespace SecretBase.Core.Widgets;

/// <summary>
/// Creates the first-run desktop layout (one Clock) without touching UI.
/// </summary>
public static class DefaultWidgetFactory
{
    public static WidgetInstance CreateDefaultClock(RoomId? roomId = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = ClockWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Type = WidgetTypes.Clock,
            Position = new WidgetPosition(48, 48),
            Size = new WidgetSize(280, 160),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }
}
