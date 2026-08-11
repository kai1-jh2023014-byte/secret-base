namespace SecretBase.Core.Desktop;

/// <summary>
/// Desktop session state for the active room. Widget instances arrive in a later milestone.
/// </summary>
public sealed class DesktopLayout
{
    public required RoomId RoomId { get; init; }

    /// <summary>Schema version for layout JSON migrations.</summary>
    public int SchemaVersion { get; init; } = 1;
}
