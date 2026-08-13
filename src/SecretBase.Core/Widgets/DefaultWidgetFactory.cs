using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Text;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Widgets;

/// <summary>
/// Creates first-run widget instances without touching UI.
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

    public static WidgetInstance CreateDefaultText(RoomId? roomId = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = TextWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Type = WidgetTypes.Text,
            Position = new WidgetPosition(48, 240),
            Size = new WidgetSize(320, 180),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates a Web Widget instance. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateWeb(
        string? url = null,
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = WebWidgetConfiguration.CreateDefault();
        if (WebUrlValidator.TryNormalize(url ?? WebWidgetConfiguration.DefaultUrl, out var normalized, out _))
        {
            config.Url = normalized!;
        }

        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Web,
            Position = new WidgetPosition(x ?? 96, y ?? 96),
            Size = new WidgetSize(width ?? 560, height ?? 360),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }
}
