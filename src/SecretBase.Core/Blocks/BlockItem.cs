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

    /// <summary>Compact rail tile (icon only, no label).</summary>
    public const double RailTileWidth = 48;
    public const double RailTileHeight = 48;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public BlockItemType Type { get; set; } = BlockItemType.Application;

    /// <summary>Absolute path to the executable, .lnk, file, or folder the user chose.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Original Desktop path when the item was hidden from Desktop. Null if it was only linked.</summary>
    public string? DesktopOriginPath { get; set; }

    /// <summary>True when Secret Base moved the Desktop original into block-items storage.</summary>
    public bool HiddenFromDesktop { get; set; }

    /// <summary>
    /// Optional user/custom icon path under AppData icons/custom.
    /// Empty = show the OS shell icon for Target at runtime (not persisted).
    /// </summary>
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
        => ClampPlacement(maxWidth, maxHeight, TileWidth, TileHeight);

    public void ClampPlacement(double maxWidth, double maxHeight, double tileWidth, double tileHeight)
    {
        if (!HasPlacement)
        {
            return;
        }

        var maxX = Math.Max(0, maxWidth - tileWidth);
        var maxY = Math.Max(0, maxHeight - tileHeight);
        X = Math.Clamp(X, 0, maxX);
        Y = Math.Clamp(Y, 0, maxY);
    }
}
