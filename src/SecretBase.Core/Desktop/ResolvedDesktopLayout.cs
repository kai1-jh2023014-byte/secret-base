namespace SecretBase.Core.Desktop;

/// <summary>
/// Transient geometry for the current display. Never persisted — derived from
/// the saved layout via <see cref="ResponsiveLayoutResolver"/>.
/// </summary>
public sealed class ResolvedDesktopLayout
{
    public DesktopDisplayContext Authored { get; init; }
    public DesktopDisplayContext Current { get; init; }

    public Dictionary<Guid, ResolvedRect> Widgets { get; } = new();
    public Dictionary<Guid, ResolvedRect> Blocks { get; } = new();

    public bool IsIdentity =>
        Math.Abs(Authored.Width - Current.Width) < 0.5
        && Math.Abs(Authored.Height - Current.Height) < 0.5;
}

public readonly struct ResolvedRect
{
    public ResolvedRect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }

    public double Right => X + Width;
    public double Bottom => Y + Height;

    public ResolvedRect WithPosition(double x, double y) => new(x, y, Width, Height);

    public ResolvedRect ClampTo(DesktopSafeArea safe, double minWidth, double minHeight)
    {
        var maxW = Math.Max(minWidth, safe.Width);
        var maxH = Math.Max(minHeight, safe.Height);
        var w = Math.Clamp(Width, minWidth, maxW);
        var h = Math.Clamp(Height, minHeight, maxH);
        var x = Math.Clamp(X, safe.Left, Math.Max(safe.Left, safe.Right - w));
        var y = Math.Clamp(Y, safe.Top, Math.Max(safe.Top, safe.Bottom - h));
        return new ResolvedRect(x, y, w, h);
    }
}
