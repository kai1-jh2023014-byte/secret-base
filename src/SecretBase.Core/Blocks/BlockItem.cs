using SecretBase.Core.Widgets;

namespace SecretBase.Core.Blocks;

/// <summary>
/// A launchable entry inside a Block (application, shortcut, file, or folder).
/// Target is an absolute path or user-chosen shell target — never a free-form command line.
/// </summary>
public sealed class BlockItem
{
    public const double TileWidth = 88;
    public const double TileHeight = 84;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public BlockItemType Type { get; set; } = BlockItemType.Application;

    /// <summary>Absolute path to the executable, .lnk, file, or folder the user chose.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Optional cached icon path (PNG). Empty = extract from Target at runtime.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Position inside the Block content canvas. Negative = needs auto-placement.</summary>
    public double X { get; set; } = -1;

    /// <summary>Position inside the Block content canvas. Negative = needs auto-placement.</summary>
    public double Y { get; set; } = -1;

    public bool HasPlacement => X >= 0 && Y >= 0;

    public void EnsurePlacement(int indexInBlock)
    {
        if (HasPlacement)
        {
            return;
        }

        const int columns = 3;
        var col = indexInBlock % columns;
        var row = indexInBlock / columns;
        X = 8 + col * (TileWidth + 8);
        Y = 8 + row * (TileHeight + 8);
    }

    public void ClampPlacement(double maxWidth, double maxHeight)
    {
        if (!HasPlacement)
        {
            return;
        }

        var maxX = Math.Max(0, maxWidth - TileWidth);
        var maxY = Math.Max(0, maxHeight - TileHeight);
        X = Math.Clamp(X, 0, maxX);
        Y = Math.Clamp(Y, 0, maxY);
    }
}
