namespace SecretBase.Core.Blocks;

/// <summary>
/// Pure press → drag-or-click state for a Block item tile.
/// Ending the gesture more than once (PointerReleased + PointerCaptureLost) yields
/// only one activation decision so a single click cannot launch repeatedly.
/// </summary>
public sealed class BlockItemPointerGesture
{
    /// <summary>Squared move threshold (px²) before a press becomes a drag. Matches 5px.</summary>
    public const double DragThresholdSquared = 25;

    public bool IsActive { get; private set; }

    public bool IsDragging { get; private set; }

    public double PressX { get; private set; }

    public double PressY { get; private set; }

    public Guid? ItemId { get; private set; }

    public void Begin(Guid itemId, double pressX, double pressY)
    {
        IsActive = true;
        IsDragging = false;
        ItemId = itemId;
        PressX = pressX;
        PressY = pressY;
    }

    /// <summary>
    /// Updates drag state from a move. Returns true when this call crosses the drag threshold.
    /// </summary>
    public bool TryMarkDragging(double x, double y)
    {
        if (!IsActive || IsDragging)
        {
            return false;
        }

        var dx = x - PressX;
        var dy = y - PressY;
        if ((dx * dx) + (dy * dy) < DragThresholdSquared)
        {
            return false;
        }

        IsDragging = true;
        return true;
    }

    /// <summary>
    /// Completes the gesture. Safe to call multiple times — only the first end counts.
    /// </summary>
    public BlockItemPointerEndResult TryEnd()
    {
        if (!IsActive)
        {
            return BlockItemPointerEndResult.Ignored;
        }

        var dragged = IsDragging;
        var itemId = ItemId;
        Clear();
        return dragged
            ? BlockItemPointerEndResult.CommitDrag(itemId!.Value)
            : BlockItemPointerEndResult.Launch(itemId!.Value);
    }

    /// <summary>
    /// Abandons the gesture without launch or drag-commit (e.g. tile rebuild on SizeChanged).
    /// </summary>
    public void Cancel() => Clear();

    private void Clear()
    {
        IsActive = false;
        IsDragging = false;
        ItemId = null;
    }
}

public readonly record struct BlockItemPointerEndResult(
    BlockItemPointerEndKind Kind,
    Guid ItemId)
{
    public static BlockItemPointerEndResult Ignored { get; } =
        new(BlockItemPointerEndKind.Ignored, Guid.Empty);

    public static BlockItemPointerEndResult Launch(Guid itemId) =>
        new(BlockItemPointerEndKind.Launch, itemId);

    public static BlockItemPointerEndResult CommitDrag(Guid itemId) =>
        new(BlockItemPointerEndKind.CommitDrag, itemId);
}

public enum BlockItemPointerEndKind
{
    Ignored = 0,
    Launch = 1,
    CommitDrag = 2
}
