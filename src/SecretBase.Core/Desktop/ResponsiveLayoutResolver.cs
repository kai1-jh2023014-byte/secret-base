using SecretBase.Core.Blocks;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Maps desktop object geometry between viewports without uniform stretch.
/// Same-width taller displays (e.g. 1920×1080 → 1920×1200) keep X/size and
/// redistribute Y via edge-preserving anchors; smaller displays clamp via
/// <see cref="DesktopViewportLayout.FitToViewport"/>.
/// </summary>
public static class ResponsiveLayoutResolver
{
    /// <summary>Relative position along an axis below which we pin the leading edge.</summary>
    public const double LeadingEdgeBias = 0.35;

    /// <summary>Relative position along an axis above which we pin the trailing edge.</summary>
    public const double TrailingEdgeBias = 0.65;

    /// <summary>
    /// Adapt layout authored for <paramref name="from"/> into <paramref name="to"/>.
    /// Mutates widget/block geometry. Returns true when any value changed.
    /// When viewports match, only an in-bounds fit runs (1920×1080 identity).
    /// </summary>
    public static bool AdaptToDisplay(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        DesktopDisplayContext from,
        DesktopDisplayContext to)
    {
        var widgetList = widgets as IList<WidgetInstance> ?? widgets.ToList();
        var blockList = blocks as IList<Block> ?? blocks.ToList();
        var changed = false;

        if (!NearlyEqual(from.Width, to.Width) || !NearlyEqual(from.Height, to.Height))
        {
            var fromSafe = from.SafeArea;
            var toSafe = to.SafeArea;
            foreach (var widget in widgetList)
            {
                changed |= RemapRect(widget.Position, widget.Size, fromSafe, toSafe);
            }

            foreach (var block in blockList)
            {
                changed |= RemapRect(block.Position, block.Size, fromSafe, toSafe);
            }
        }

        changed |= DesktopViewportLayout.FitToViewport(
            widgetList,
            blockList,
            to.Width,
            to.Height,
            margin: to.Margin,
            bottomReserve: to.BottomReserve);

        return changed;
    }

    /// <summary>
    /// Convenience overload using explicit DIP sizes (dpiScale defaults to 1).
    /// </summary>
    public static bool AdaptToDisplay(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        double fromWidth,
        double fromHeight,
        double toWidth,
        double toHeight,
        double margin = DesktopLayoutReference.Margin,
        double bottomReserve = DesktopLayoutReference.BottomReserve,
        double dpiScale = 1.0)
    {
        var from = new DesktopDisplayContext(fromWidth, fromHeight, dpiScale, margin, bottomReserve);
        var to = new DesktopDisplayContext(toWidth, toHeight, dpiScale, margin, bottomReserve);
        return AdaptToDisplay(widgets, blocks, from, to);
    }

    public static LayoutAnchor InferAnchor(
        WidgetPosition position,
        WidgetSize size,
        DesktopSafeArea safe)
    {
        var spanX = Math.Max(1, safe.Width - size.Width);
        var spanY = Math.Max(1, safe.Height - size.Height);
        var tx = Math.Clamp((position.X - safe.Left) / spanX, 0, 1);
        var ty = Math.Clamp((position.Y - safe.Top) / spanY, 0, 1);

        var horizontal = tx <= LeadingEdgeBias
            ? -1
            : tx >= TrailingEdgeBias ? 1 : 0;
        var vertical = ty <= LeadingEdgeBias
            ? -1
            : ty >= TrailingEdgeBias ? 1 : 0;

        return (horizontal, vertical) switch
        {
            (-1, -1) => LayoutAnchor.TopLeft,
            (0, -1) => LayoutAnchor.TopCenter,
            (1, -1) => LayoutAnchor.TopRight,
            (-1, 0) => LayoutAnchor.Center,
            (0, 0) => LayoutAnchor.Center,
            (1, 0) => LayoutAnchor.Center,
            (-1, 1) => LayoutAnchor.BottomLeft,
            (0, 1) => LayoutAnchor.BottomCenter,
            (1, 1) => LayoutAnchor.BottomRight,
            _ => LayoutAnchor.Center
        };
    }

    private static bool RemapRect(
        WidgetPosition position,
        WidgetSize size,
        DesktopSafeArea fromSafe,
        DesktopSafeArea toSafe)
    {
        // Never grow widgets when the display grows — only reposition.
        // FitToViewport may shrink later if the target is smaller.
        var width = size.Width;
        var height = size.Height;

        var leadingX = position.X - fromSafe.Left;
        var trailingX = fromSafe.Right - (position.X + width);
        var leadingY = position.Y - fromSafe.Top;
        var trailingY = fromSafe.Bottom - (position.Y + height);

        var nextX = MapAxis(
            leadingX,
            trailingX,
            width,
            fromSafe.Width,
            toSafe.Width,
            toSafe.Left);
        var nextY = MapAxis(
            leadingY,
            trailingY,
            height,
            fromSafe.Height,
            toSafe.Height,
            toSafe.Top);

        var changed = false;
        if (!NearlyEqual(position.X, nextX) || !NearlyEqual(position.Y, nextY))
        {
            position.X = nextX;
            position.Y = nextY;
            changed = true;
        }

        // Size intentionally unchanged here (no uniform scale).
        _ = width;
        _ = height;
        return changed;
    }

    /// <summary>
    /// Map one axis: pin leading/trailing edges when clearly biased; otherwise
    /// keep relative position. Identical usable length → keep leading offset.
    /// </summary>
    public static double MapAxis(
        double leading,
        double trailing,
        double size,
        double fromUsable,
        double toUsable,
        double toOrigin)
    {
        if (NearlyEqual(fromUsable, toUsable))
        {
            return toOrigin + leading;
        }

        var fromSpan = fromUsable - size;
        if (fromSpan <= 1)
        {
            return toOrigin;
        }

        var t = Math.Clamp(leading / fromSpan, 0, 1);
        var toSpan = Math.Max(0, toUsable - size);

        if (t <= LeadingEdgeBias)
        {
            // Top / left: keep inset so 1080→1200 does not push header objects down.
            return toOrigin + Math.Clamp(leading, 0, toSpan);
        }

        if (t >= TrailingEdgeBias)
        {
            // Bottom / right: preserve distance to the trailing safe edge.
            var keepTrailing = Math.Max(0, trailing);
            return toOrigin + Math.Max(0, toSpan - keepTrailing);
        }

        // Middle band: proportional — uses extra vertical space without enlarging widgets.
        return toOrigin + t * toSpan;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.5;
}
