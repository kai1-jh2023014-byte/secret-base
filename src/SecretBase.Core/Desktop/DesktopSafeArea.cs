namespace SecretBase.Core.Desktop;

/// <summary>
/// Widget-placeable rectangle inside the work area, excluding chrome such as
/// the bottom AI shelf / control strip reserve.
/// </summary>
public readonly struct DesktopSafeArea
{
    public DesktopSafeArea(double left, double top, double right, double bottom)
    {
        Left = left;
        Top = top;
        Right = Math.Max(left, right);
        Bottom = Math.Max(top, bottom);
    }

    public double Left { get; }
    public double Top { get; }
    public double Right { get; }
    public double Bottom { get; }

    public double Width => Math.Max(0, Right - Left);
    public double Height => Math.Max(0, Bottom - Top);

    public static DesktopSafeArea From(DesktopDisplayContext context)
    {
        var pad = context.Margin;
        var bottom = Math.Max(pad, context.Height - context.BottomReserve - pad);
        var right = Math.Max(pad, context.Width - pad);
        return new DesktopSafeArea(pad, pad, right, bottom);
    }
}
