using SecretBase.Core.Blocks;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Maps saved (authored) desktop geometry into a transient resolved layout for
/// the current display. Never mutates the saved layout — callers must apply
/// <see cref="ResolvedDesktopLayout"/> to the UI only.
/// </summary>
public static class ResponsiveLayoutResolver
{
    public const double LeadingEdgeBias = 0.35;
    public const double TrailingEdgeBias = 0.65;
    private const double AxisEpsilon = 0.5;
    private const double OverlapGap = 12;

    /// <summary>
    /// Pure resolve: saved geometry → current display. Input positions/sizes are
    /// not modified. Same authored/current viewport yields an identity mapping
    /// (plus in-bounds clamp).
    /// </summary>
    public static ResolvedDesktopLayout Resolve(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        DesktopDisplayContext authored,
        DesktopDisplayContext current)
    {
        var result = new ResolvedDesktopLayout
        {
            Authored = authored,
            Current = current
        };

        var authoredSafe = authored.SafeArea;
        var currentSafe = current.SafeArea;
        var sameWidth = NearlyEqual(authoredSafe.Width, currentSafe.Width);
        var sameHeight = NearlyEqual(authoredSafe.Height, currentSafe.Height);
        var identity = sameWidth && sameHeight;

        foreach (var widget in widgets)
        {
            var rect = identity
                ? new ResolvedRect(widget.Position.X, widget.Position.Y, widget.Size.Width, widget.Size.Height)
                : RemapRect(
                    widget.Position.X,
                    widget.Position.Y,
                    widget.Size.Width,
                    widget.Size.Height,
                    authoredSafe,
                    currentSafe,
                    sameWidth,
                    sameHeight);
            result.Widgets[widget.Id] = rect.ClampTo(currentSafe, minWidth: 120, minHeight: 80);
        }

        foreach (var block in blocks)
        {
            var minW = BlockLayoutMode.IsRail(block.LayoutMode) ? Block.RailMinWidth : Block.MinWidth;
            var minH = BlockLayoutMode.IsRail(block.LayoutMode) ? Block.RailMinHeight : Block.MinHeight;
            var rect = identity
                ? new ResolvedRect(block.Position.X, block.Position.Y, block.Size.Width, block.Size.Height)
                : RemapRect(
                    block.Position.X,
                    block.Position.Y,
                    block.Size.Width,
                    block.Size.Height,
                    authoredSafe,
                    currentSafe,
                    sameWidth,
                    sameHeight);
            result.Blocks[block.Id] = rect.ClampTo(currentSafe, minW, minH);
        }

        if (!identity)
        {
            SeparateOverlaps(result, currentSafe);
        }

        return result;
    }

    public static ResolvedDesktopLayout Resolve(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        double authoredWidth,
        double authoredHeight,
        double currentWidth,
        double currentHeight,
        double margin = DesktopLayoutReference.Margin,
        double bottomReserve = DesktopLayoutReference.BottomReserve,
        double dpiScale = 1.0)
    {
        var authored = new DesktopDisplayContext(authoredWidth, authoredHeight, dpiScale, margin, bottomReserve);
        var current = new DesktopDisplayContext(currentWidth, currentHeight, dpiScale, margin, bottomReserve);
        return Resolve(widgets, blocks, authored, current);
    }

    /// <summary>
    /// Mutating helper for tests / explicit rebases. Prefer <see cref="Resolve"/> for display.
    /// </summary>
    public static bool AdaptToDisplay(
        IEnumerable<WidgetInstance> widgets,
        IEnumerable<Block> blocks,
        DesktopDisplayContext from,
        DesktopDisplayContext to)
    {
        var widgetList = widgets as IList<WidgetInstance> ?? widgets.ToList();
        var blockList = blocks as IList<Block> ?? blocks.ToList();
        var resolved = Resolve(widgetList, blockList, from, to);
        var changed = false;

        foreach (var widget in widgetList)
        {
            if (!resolved.Widgets.TryGetValue(widget.Id, out var rect))
            {
                continue;
            }

            changed |= ApplyRect(widget.Position, widget.Size, rect);
        }

        foreach (var block in blockList)
        {
            if (!resolved.Blocks.TryGetValue(block.Id, out var rect))
            {
                continue;
            }

            changed |= ApplyRect(block.Position, block.Size, rect);
        }

        return changed;
    }

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
            (-1, 1) => LayoutAnchor.BottomLeft,
            (0, 1) => LayoutAnchor.BottomCenter,
            (1, 1) => LayoutAnchor.BottomRight,
            _ => LayoutAnchor.Center
        };
    }

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
            return toOrigin + Math.Clamp(leading, 0, toSpan);
        }

        if (t >= TrailingEdgeBias)
        {
            var keepTrailing = Math.Max(0, trailing);
            return toOrigin + Math.Max(0, toSpan - keepTrailing);
        }

        return toOrigin + t * toSpan;
    }

    private static ResolvedRect RemapRect(
        double x,
        double y,
        double width,
        double height,
        DesktopSafeArea fromSafe,
        DesktopSafeArea toSafe,
        bool sameWidth,
        bool sameHeight)
    {
        // Same width: keep X exactly (no horizontal drift on 1920×1080 → 1920×1200).
        var nextX = sameWidth
            ? x
            : MapAxis(
                x - fromSafe.Left,
                fromSafe.Right - (x + width),
                width,
                fromSafe.Width,
                toSafe.Width,
                toSafe.Left);

        var nextY = sameHeight
            ? y
            : MapAxis(
                y - fromSafe.Top,
                fromSafe.Bottom - (y + height),
                height,
                fromSafe.Height,
                toSafe.Height,
                toSafe.Top);

        // Never grow sizes when the display grows.
        return new ResolvedRect(nextX, nextY, width, height);
    }

    private static void SeparateOverlaps(ResolvedDesktopLayout layout, DesktopSafeArea safe)
    {
        var items = new List<(Guid Id, bool IsBlock, ResolvedRect Rect)>();
        foreach (var (id, rect) in layout.Widgets)
        {
            items.Add((id, false, rect));
        }

        foreach (var (id, rect) in layout.Blocks)
        {
            items.Add((id, true, rect));
        }

        items.Sort((a, b) => a.Rect.Y.CompareTo(b.Rect.Y));

        for (var i = 1; i < items.Count; i++)
        {
            var prev = items[i - 1].Rect;
            var cur = items[i].Rect;
            if (!Intersects(prev, cur))
            {
                continue;
            }

            var pushedY = prev.Bottom + OverlapGap;
            var maxY = Math.Max(safe.Top, safe.Bottom - cur.Height);
            if (pushedY <= maxY)
            {
                cur = cur.WithPosition(cur.X, pushedY);
            }
            else
            {
                // Prefer keeping bottom widgets; nudge the upper one up when needed.
                var prevY = Math.Max(safe.Top, cur.Y - cur.Height - OverlapGap);
                prev = prev.WithPosition(prev.X, Math.Min(prev.Y, prevY));
                items[i - 1] = (items[i - 1].Id, items[i - 1].IsBlock, prev);
                WriteBack(layout, items[i - 1]);
                continue;
            }

            items[i] = (items[i].Id, items[i].IsBlock, cur);
            WriteBack(layout, items[i]);
        }
    }

    private static void WriteBack(
        ResolvedDesktopLayout layout,
        (Guid Id, bool IsBlock, ResolvedRect Rect) item)
    {
        if (item.IsBlock)
        {
            layout.Blocks[item.Id] = item.Rect;
        }
        else
        {
            layout.Widgets[item.Id] = item.Rect;
        }
    }

    private static bool Intersects(ResolvedRect a, ResolvedRect b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

    private static bool ApplyRect(WidgetPosition position, WidgetSize size, ResolvedRect rect)
    {
        var changed = false;
        if (!NearlyEqual(position.X, rect.X) || !NearlyEqual(position.Y, rect.Y))
        {
            position.X = rect.X;
            position.Y = rect.Y;
            changed = true;
        }

        if (!NearlyEqual(size.Width, rect.Width) || !NearlyEqual(size.Height, rect.Height))
        {
            size.Width = rect.Width;
            size.Height = rect.Height;
            changed = true;
        }

        return changed;
    }

    private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < AxisEpsilon;
}
