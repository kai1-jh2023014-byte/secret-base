using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Blocks;

public static class DefaultBlockFactory
{
    public static Block Create(
        string name,
        RoomId? roomId = null,
        double? x = null,
        double? y = null,
        double? width = null,
        double? height = null)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "New Block" : name.Trim();
        var block = new Block
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            RoomId = roomId ?? RoomId.DefaultRoomId,
            Position = new WidgetPosition(x ?? 420, y ?? 48),
            Size = new WidgetSize(width ?? Block.DefaultWidth, height ?? Block.DefaultHeight),
            Theme = null,
            Items = []
        };
        block.ClampSize();
        return block;
    }
}
