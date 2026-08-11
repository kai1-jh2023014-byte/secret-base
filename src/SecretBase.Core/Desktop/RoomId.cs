namespace SecretBase.Core.Desktop;

/// <summary>
/// Identifies a future Room. v0.1 always uses <see cref="DefaultRoomId"/>.
/// Layout/theme data is keyed by room so Room feature can land without migration pain.
/// </summary>
public sealed record RoomId(string Value)
{
    public static RoomId DefaultRoomId { get; } = new("default");

    public override string ToString() => Value;
}
