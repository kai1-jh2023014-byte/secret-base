using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Core.Widgets.Apps;
using SecretBase.Core.Widgets.Assistant;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Creative;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Text;
using SecretBase.Core.Widgets.Web;
using SecretBase.Core.Widgets.Dashboard;
using SecretBase.Core.Widgets.Workspace;

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
        config.DisplayStyle = ClockWidgetConfiguration.StyleBase;
        config.ShowSeconds = false;
        return new WidgetInstance
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Type = WidgetTypes.Clock,
            Position = new WidgetPosition(48, 48),
            Size = new WidgetSize(280, 200),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>Creates an additional Clock widget (not the seeded default id).</summary>
    public static WidgetInstance CreateClock(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = ClockWidgetConfiguration.CreateDefault();
        config.DisplayStyle = ClockWidgetConfiguration.StyleBase;
        config.ShowSeconds = false;
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Clock,
            Position = new WidgetPosition(x ?? 48, y ?? 48),
            Size = new WidgetSize(width ?? 280, height ?? 200),
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

    /// <summary>Creates an additional Text widget (not the seeded default id).</summary>
    public static WidgetInstance CreateText(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        TextWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? TextWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Text,
            Position = new WidgetPosition(x ?? 48, y ?? 240),
            Size = new WidgetSize(width ?? 320, height ?? 180),
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

    /// <summary>
    /// Creates a Calendar Widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateCalendar(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        CalendarWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? CalendarWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Calendar,
            Position = new WidgetPosition(x ?? 360, y ?? 48),
            Size = new WidgetSize(width ?? 320, height ?? 360),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates a Music Widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateMusic(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        MusicWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? MusicWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Music,
            Position = new WidgetPosition(x ?? 120, y ?? 120),
            Size = new WidgetSize(width ?? 360, height ?? 420),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates a Creative Workspace Widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateCreative(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        CreativeWorkspaceWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? CreativeWorkspaceWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Creative,
            Position = new WidgetPosition(x ?? 200, y ?? 80),
            Size = new WidgetSize(width ?? 340, height ?? 440),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates an AI Workspace Widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateAi(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        AiWorkspaceWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? AiWorkspaceWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Ai,
            Position = new WidgetPosition(x ?? 240, y ?? 100),
            Size = new WidgetSize(width ?? 300, height ?? 420),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates a My Apps widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateApps(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        AppsWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? AppsWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Apps,
            Position = new WidgetPosition(x ?? 280, y ?? 80),
            Size = new WidgetSize(width ?? 320, height ?? 420),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>
    /// Creates a Secret Base AI chat widget. Not seeded into default layout — add explicitly.
    /// </summary>
    public static WidgetInstance CreateAssistant(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        AssistantWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? AssistantWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Assistant,
            Position = new WidgetPosition(x ?? 320, y ?? 60),
            Size = new WidgetSize(width ?? 360, height ?? 480),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>Creates a Workspace widget. Not seeded into default layout — add explicitly or via onboarding.</summary>
    public static WidgetInstance CreateWorkspace(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        WorkspaceWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? WorkspaceWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Workspace,
            Position = new WidgetPosition(x ?? 360, y ?? 80),
            Size = new WidgetSize(width ?? 340, height ?? 420),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }

    /// <summary>Creates a Base dashboard Mini App. Not seeded into default layout.</summary>
    public static WidgetInstance CreateDashboard(
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null,
        DashboardWidgetConfiguration? configuration = null)
    {
        var room = roomId ?? RoomId.DefaultRoomId;
        var config = configuration ?? DashboardWidgetConfiguration.CreateDefault();
        return new WidgetInstance
        {
            Id = Guid.NewGuid(),
            Type = WidgetTypes.Dashboard,
            Position = new WidgetPosition(x ?? 48, y ?? 270),
            Size = new WidgetSize(width ?? 340, height ?? 380),
            RoomId = room,
            Configuration = config.ToDictionary()
        };
    }
}
