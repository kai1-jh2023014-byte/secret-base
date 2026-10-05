namespace SecretBase.Core.Blocks;

/// <summary>
/// Placement helpers for Block items inside an available content area.
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

    /// <summary>
    /// Fashionable compact rail: single column when taller than wide, otherwise a single row.
    /// </summary>
    public static void ArrangeRail(IReadOnlyList<BlockItem> items, double areaWidth, double areaHeight)
    {
        if (items.Count == 0)
        {
            return;
        }

        const double pad = 6;
        const double gap = 10;
        var tileW = BlockItem.RailTileWidth;
        var tileH = BlockItem.RailTileHeight;
        var width = Math.Max(tileW + pad * 2, areaWidth);
        var height = Math.Max(tileH + pad * 2, areaHeight);
        var vertical = height >= width;

        if (vertical)
        {
            var totalH = items.Count * tileH + Math.Max(0, items.Count - 1) * gap;
            var startY = pad + Math.Max(0, (height - pad * 2 - totalH) / 2);
            var x = pad + Math.Max(0, (width - pad * 2 - tileW) / 2);
            for (var i = 0; i < items.Count; i++)
            {
                items[i].X = x;
                items[i].Y = startY + i * (tileH + gap);
                items[i].ClampPlacement(width, height, tileW, tileH);
            }

            return;
        }

        var totalW = items.Count * tileW + Math.Max(0, items.Count - 1) * gap;
        var startX = pad + Math.Max(0, (width - pad * 2 - totalW) / 2);
        var y = pad + Math.Max(0, (height - pad * 2 - tileH) / 2);
        for (var i = 0; i < items.Count; i++)
        {
            items[i].X = startX + i * (tileW + gap);
            items[i].Y = y;
            items[i].ClampPlacement(width, height, tileW, tileH);
        }
    }
}
