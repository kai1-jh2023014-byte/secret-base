namespace SecretBase.Core.Blocks;

/// <summary>
/// Even grid placement for Block items inside an available content area.
/// </summary>
public static class BlockItemLayout
{
    public static void ArrangeEvenly(IReadOnlyList<BlockItem> items, double areaWidth, double areaHeight)
    {
        if (items.Count == 0)
        {
            return;
        }

        const double pad = 8;
        var width = Math.Max(BlockItem.TileWidth + pad * 2, areaWidth);
        var height = Math.Max(BlockItem.TileHeight + pad * 2, areaHeight);
        var usableW = width - pad * 2;
        var usableH = height - pad * 2;

        var maxCols = Math.Max(1, (int)Math.Floor((usableW + pad) / (BlockItem.TileWidth + pad)));
        var cols = Math.Min(maxCols, items.Count);
        var rows = (int)Math.Ceiling(items.Count / (double)cols);

        var cellW = usableW / cols;
        var cellH = usableH / Math.Max(1, rows);

        for (var i = 0; i < items.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var x = pad + col * cellW + Math.Max(0, (cellW - BlockItem.TileWidth) / 2);
            var y = pad + row * cellH + Math.Max(0, (cellH - BlockItem.TileHeight) / 2);
            items[i].X = x;
            items[i].Y = y;
            items[i].ClampPlacement(width, height);
        }
    }
}
