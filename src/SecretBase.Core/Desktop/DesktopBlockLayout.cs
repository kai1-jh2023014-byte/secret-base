using SecretBase.Core.Blocks;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Neat grid placement for Desktop Blocks (compact equal gaps from top-left).
/// </summary>
public static class DesktopBlockLayout
{
    public static void ArrangeEvenly(
        IReadOnlyList<Block> blocks,
        double areaWidth,
        double areaHeight,
        double margin = 24,
        double gap = 28)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var width = Math.Max(Block.MinWidth, areaWidth);
        var height = Math.Max(Block.MinHeight, areaHeight);
        var pad = Math.Max(8, margin);
        var spacing = Math.Max(8, gap);

        var cellW = blocks.Max(b => Math.Max(Block.MinWidth, b.Size.Width));
        var cellH = blocks.Max(b => Math.Max(Block.MinHeight, b.Size.Height));
        var usableW = Math.Max(cellW, width - pad * 2);

        var maxCols = Math.Max(1, (int)Math.Floor((usableW + spacing) / (cellW + spacing)));
        var cols = Math.Min(maxCols, blocks.Count);

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var col = i % cols;
            var row = i / cols;
            var cellLeft = pad + col * (cellW + spacing);
            var cellTop = pad + row * (cellH + spacing);
            var x = cellLeft + Math.Max(0, (cellW - block.Size.Width) / 2);
            var y = cellTop + Math.Max(0, (cellH - block.Size.Height) / 2);
            block.Position.X = Math.Clamp(x, 0, Math.Max(0, width - block.Size.Width));
            block.Position.Y = Math.Clamp(y, 0, Math.Max(0, height - block.Size.Height));
        }
    }

    /// <summary>
    /// Places Blocks in an even grid starting below <paramref name="topOffset"/>
    /// so they sit under an already-arranged Widget band.
    /// </summary>
    public static void ArrangeEvenlyBelow(
        IReadOnlyList<Block> blocks,
        double areaWidth,
        double areaHeight,
        double topOffset,
        double margin = 24,
        double gap = 28)
    {
        if (blocks.Count == 0)
        {
            return;
        }

        var remainingHeight = Math.Max(Block.MinHeight, areaHeight - topOffset);
        ArrangeEvenly(blocks, areaWidth, remainingHeight, margin, gap);
        foreach (var block in blocks)
        {
            block.Position.Y += topOffset;
            block.Position.Y = Math.Clamp(
                block.Position.Y,
                0,
                Math.Max(0, areaHeight - block.Size.Height));
        }
    }
}
