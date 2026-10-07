namespace SecretBase.Core.Blocks;

/// <summary>
/// Resolved tile / icon sizes for a Block content area. When the Block is
/// smaller than the preferred grid, <see cref="Scale"/> drops below 1 so
/// icons shrink instead of overlapping.
/// </summary>
public readonly struct BlockItemTileMetrics
{
    public BlockItemTileMetrics(
        double tileWidth,
        double tileHeight,
        double iconSize,
        double iconHostSize,
        double scale,
        bool showLabels)
    {
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        IconSize = iconSize;
        IconHostSize = iconHostSize;
        Scale = scale;
        ShowLabels = showLabels;
    }

    public double TileWidth { get; }
    public double TileHeight { get; }
    public double IconSize { get; }
    public double IconHostSize { get; }
    public double Scale { get; }
    public bool ShowLabels { get; }

    public static BlockItemTileMetrics ForGrid(bool showLabels, double scale = 1.0)
    {
        scale = Math.Clamp(scale, BlockItemLayout.MinScale, 1.0);
        var tileW = BlockItem.TileWidth * scale;
        var tileH = (showLabels ? BlockItem.TileHeight : BlockItem.IconOnlyTileHeight) * scale;
        return new BlockItemTileMetrics(
            tileW,
            tileH,
            iconSize: 36 * scale,
            iconHostSize: 40 * scale,
            scale,
            showLabels);
    }

    public static BlockItemTileMetrics ForRail(double scale = 1.0)
    {
        scale = Math.Clamp(scale, BlockItemLayout.MinScale, 1.0);
        var tile = BlockItem.RailTileWidth * scale;
        return new BlockItemTileMetrics(
            tile,
            tile,
            iconSize: 32 * scale,
            iconHostSize: 40 * scale,
            scale,
            showLabels: false);
    }
}
