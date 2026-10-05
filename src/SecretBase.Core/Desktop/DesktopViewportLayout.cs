using SecretBase.Core.Blocks;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Keeps widgets and Blocks inside the visible work area when the display
/// size or aspect ratio changes (e.g. Remote Desktop session resize).
/// </summary>
public static class DesktopViewportLayout
{
    public const double DefaultMargin = 16;
    public const double DefaultBottomReserve = 72;

    /// <summary>
    /// Clamps size and position of every desktop object into
    /// <paramref name="areaWidth"/> × (<paramref name="areaHeight"/> − bottom reserve).
    /// Returns true when any geometry changed.
    /// </summary>
    public static bool FitToViewport(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        double areaWidth,
        double areaHeight,
        double margin = DefaultMargin,
        double bottomReserve = DefaultBottomReserve)
    {
        var changed = false;
        var width = Math.Max(160, areaWidth);
        var usableHeight = Math.Max(120, areaHeight - Math.Max(0, bottomReserve));
        var pad = Math.Max(0, margin);

        foreach (var widget in widgets)
        {
            changed |= FitRect(
                widget.Position,
                widget.Size,
                width,
                usableHeight,
                pad,
                minWidth: 120,
                minHeight: 80);
        }

        foreach (var block in blocks)
        {
            changed |= FitRect(
                block.Position,
                block.Size,
                width,
                usableHeight,
                pad,
                minWidth: Block.MinWidth,
                minHeight: Block.MinHeight);
        }

        return changed;
    }

    private static bool FitRect(
        WidgetPosition position,
        WidgetSize size,
        double areaWidth,
        double areaHeight,
        double pad,
        double minWidth,
        double minHeight)
    {
        var changed = false;
        var maxWidth = Math.Max(minWidth, areaWidth - pad * 2);
        var maxHeight = Math.Max(minHeight, areaHeight - pad * 2);
        var previousWidth = size.Width;
        var previousHeight = size.Height;
        size.Clamp(minWidth, minHeight, maxWidth, maxHeight);
        if (!NearlyEqual(previousWidth, size.Width) || !NearlyEqual(previousHeight, size.Height))
        {
            changed = true;
        }

        var maxX = Math.Max(pad, areaWidth - size.Width - pad);
        var maxY = Math.Max(pad, areaHeight - size.Height - pad);
        var nextX = Math.Clamp(position.X, pad, maxX);
        var nextY = Math.Clamp(position.Y, pad, maxY);
        if (!NearlyEqual(position.X, nextX) || !NearlyEqual(position.Y, nextY))
        {
            position.X = nextX;
            position.Y = nextY;
            changed = true;
        }

        return changed;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.5;
}
