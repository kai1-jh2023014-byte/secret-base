using SecretBase.Core.Blocks;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Desktop session state for a Room. Widget instances and Blocks are UI-agnostic.
/// Geometry is stored in DIP for the last known <see cref="LayoutViewportWidth"/> ×
/// <see cref="LayoutViewportHeight"/>; <see cref="ResponsiveLayoutResolver"/> remaps
/// when the host work area changes.
/// </summary>
public sealed class DesktopLayout
{
    /// <summary>Current layout schema. V2 adds Blocks; V3 adds viewport metadata.</summary>
    public const int CurrentSchemaVersion = 3;

    public RoomId RoomId { get; set; } = RoomId.DefaultRoomId;

    /// <summary>Schema version for layout JSON migrations.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Design reference width (DIP). Defaults to <see cref="DesktopLayoutReference.Width"/>.
    /// Does not force remapping by itself; see <see cref="LayoutViewportWidth"/>.
    /// </summary>
    public double ReferenceWidth { get; set; } = DesktopLayoutReference.Width;

    /// <summary>
    /// Design reference height (DIP). Defaults to <see cref="DesktopLayoutReference.Height"/>.
    /// </summary>
    public double ReferenceHeight { get; set; } = DesktopLayoutReference.Height;

    /// <summary>
    /// Viewport width (DIP) for which <see cref="Widgets"/>/<see cref="Blocks"/> coordinates
    /// are currently authored. Null = not yet bound (adopted on first fit).
    /// </summary>
    public double? LayoutViewportWidth { get; set; }

    /// <summary>
    /// Viewport height (DIP) for which geometry is currently authored.
    /// </summary>
    public double? LayoutViewportHeight { get; set; }

    public List<WidgetInstance> Widgets { get; set; } = [];

    /// <summary>Desktop "rooms" that hold launchable items. Empty on upgraded v1 layouts.</summary>
    public List<Block> Blocks { get; set; } = [];

    public static DesktopLayout CreateDefault()
    {
        return new DesktopLayout
        {
            RoomId = RoomId.DefaultRoomId,
            SchemaVersion = CurrentSchemaVersion,
            ReferenceWidth = DesktopLayoutReference.Width,
            ReferenceHeight = DesktopLayoutReference.Height,
            // Seed geometry is authored for the reference canvas.
            LayoutViewportWidth = DesktopLayoutReference.Width,
            LayoutViewportHeight = DesktopLayoutReference.Height,
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
