using SecretBase.Core.Blocks;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Desktop session state for a Room. Widget instances and Blocks are UI-agnostic.
/// </summary>
public sealed class DesktopLayout
{
    /// <summary>Current layout schema. V2 adds <see cref="Blocks"/>.</summary>
    public const int CurrentSchemaVersion = 2;

    public RoomId RoomId { get; set; } = RoomId.DefaultRoomId;

    /// <summary>Schema version for layout JSON migrations.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<WidgetInstance> Widgets { get; set; } = [];

    /// <summary>Desktop "rooms" that hold launchable items. Empty on upgraded v1 layouts.</summary>
    public List<Block> Blocks { get; set; } = [];

    public static DesktopLayout CreateDefault()
    {
        return new DesktopLayout
        {
            RoomId = RoomId.DefaultRoomId,
            SchemaVersion = CurrentSchemaVersion,
            // First-run seed: Clock + Text. Add more via Add Widget (+).
            Widgets =
            [
                DefaultWidgetFactory.CreateDefaultClock(),
                DefaultWidgetFactory.CreateDefaultText()
            ],
            Blocks = []
        };
    }
}
