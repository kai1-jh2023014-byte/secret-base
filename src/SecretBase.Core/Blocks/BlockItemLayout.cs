namespace SecretBase.Core.Blocks;

/// <summary>
/// Placement helpers for Block items inside an available content area.
/// Shrinks tiles when the area would otherwise force overlap.
/// </summary>
public static class BlockItemLayout
{
    public const double MinScale = 0.35;

    public static BlockItemTileMetrics ArrangeEvenly(
        IReadOnlyList<BlockItem> items,
        double areaWidth,
        double areaHeight,
        bool showLabels = true)
    {
        var metrics = BlockItemTileMetrics.ForGrid(showLabels, scale: 1.0);
        if (items.Count == 0)
        {
            return metrics;
        }

        const double pad = 8;
        var preferredW = BlockItem.TileWidth;
        var preferredH = showLabels ? BlockItem.TileHeight : BlockItem.IconOnlyTileHeight;
        var width = Math.Max(preferredW * MinScale + pad * 2, areaWidth);
        var height = Math.Max(preferredH * MinScale + pad * 2, areaHeight);
        var usableW = Math.Max(1, width - pad * 2);
        var usableH = Math.Max(1, height - pad * 2);

        var cols = ChooseColumnCount(items.Count, usableW, usableH, preferredW, preferredH, pad);
        var rows = (int)Math.Ceiling(items.Count / (double)cols);
        var cellW = usableW / cols;
        var cellH = usableH / Math.Max(1, rows);

        var scale = Math.Min(1.0, Math.Min(cellW / preferredW, cellH / preferredH));
        scale = Math.Max(MinScale, scale);
        metrics = BlockItemTileMetrics.ForGrid(showLabels, scale);
        var tileW = metrics.TileWidth;
        var tileH = metrics.TileHeight;

        for (var i = 0; i < items.Count; i++)
        {
            var col = i % cols;
            var row = i / cols;
            var x = pad + col * cellW + Math.Max(0, (cellW - tileW) / 2);
            var y = pad + row * cellH + Math.Max(0, (cellH - tileH) / 2);
            items[i].X = x;
            items[i].Y = y;
            items[i].ClampPlacement(width, height, tileW, tileH);
        }

        return metrics;
    }

    /// <summary>
    /// Fashionable compact rail: single column when taller than wide, otherwise a single row.
    /// </summary>
    public static BlockItemTileMetrics ArrangeRail(
        IReadOnlyList<BlockItem> items,
        double areaWidth,
        double areaHeight)
    {
        var metrics = BlockItemTileMetrics.ForRail(scale: 1.0);
        if (items.Count == 0)
        {
            return metrics;
        }

        const double pad = 6;
        const double preferredGap = 10;
        var preferredTile = BlockItem.RailTileWidth;
        var width = Math.Max(preferredTile * MinScale + pad * 2, areaWidth);
        var height = Math.Max(preferredTile * MinScale + pad * 2, areaHeight);
        var vertical = height >= width;
        var usable = (vertical ? height : width) - pad * 2;
        var needed = items.Count * preferredTile + Math.Max(0, items.Count - 1) * preferredGap;
        var scale = needed <= 0 ? 1.0 : Math.Min(1.0, usable / needed);
        scale = Math.Max(MinScale, scale);
        metrics = BlockItemTileMetrics.ForRail(scale);
        var tileW = metrics.TileWidth;
        var tileH = metrics.TileHeight;
        var gap = preferredGap * scale;

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

            return metrics;
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

        return metrics;
    }

    /// <summary>
    /// Prefer the classic "as many full-size columns as fit" layout. When that
    /// would force scale &lt; 1, search for the column count that maximizes scale.
    /// </summary>
    internal static int ChooseColumnCount(
        int count,
        double usableW,
        double usableH,
        double preferredW,
        double preferredH,
        double pad)
    {
        if (count <= 1)
        {
            return 1;
        }

        var maxColsFit = Math.Max(1, (int)Math.Floor((usableW + pad) / (preferredW + pad)));
        var preferredCols = Math.Min(maxColsFit, count);
        if (ScaleForGrid(count, preferredCols, usableW, usableH, preferredW, preferredH) >= 1.0 - 1e-9)
        {
            return preferredCols;
        }

        var bestCols = preferredCols;
        var bestScale = double.NegativeInfinity;
        for (var cols = 1; cols <= count; cols++)
        {
            var scale = ScaleForGrid(count, cols, usableW, usableH, preferredW, preferredH);
            // Prefer higher scale; when tied, prefer more columns (wider row).
            if (scale > bestScale + 1e-9 || (Math.Abs(scale - bestScale) <= 1e-9 && cols > bestCols))
            {
                bestScale = scale;
                bestCols = cols;
            }
        }

        return bestCols;
    }

    private static double ScaleForGrid(
        int count,
        int cols,
        double usableW,
        double usableH,
        double preferredW,
        double preferredH)
    {
        var rows = (int)Math.Ceiling(count / (double)Math.Max(1, cols));
        var cellW = usableW / Math.Max(1, cols);
        var cellH = usableH / Math.Max(1, rows);
        return Math.Min(1.0, Math.Min(cellW / preferredW, cellH / preferredH));
    }
}
