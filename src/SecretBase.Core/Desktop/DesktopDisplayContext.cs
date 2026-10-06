namespace SecretBase.Core.Desktop;

/// <summary>
/// Current host display metrics in WinUI DIP (effective pixels).
/// DPI scale is recorded for diagnostics; layout math stays in DIP space because
/// XAML <c>ActualWidth</c>/<c>ActualHeight</c> are already DPI-normalized.
/// </summary>
public readonly struct DesktopDisplayContext
{
    public DesktopDisplayContext(
        double width,
        double height,
        double dpiScale = 1.0,
        double margin = DesktopLayoutReference.Margin,
        double bottomReserve = DesktopLayoutReference.BottomReserve)
    {
        Width = Math.Max(160, width);
        Height = Math.Max(120, height);
        DpiScale = dpiScale <= 0 ? 1.0 : dpiScale;
        Margin = Math.Max(0, margin);
        BottomReserve = Math.Max(0, bottomReserve);
    }

    public double Width { get; }
    public double Height { get; }

    /// <summary>Rasterization / effective DPI scale (1.0 = 100%).</summary>
    public double DpiScale { get; }

    public double Margin { get; }
    public double BottomReserve { get; }

    public DesktopSafeArea SafeArea => DesktopSafeArea.From(this);

    public static DesktopDisplayContext Reference(double dpiScale = 1.0) =>
        new(DesktopLayoutReference.Width, DesktopLayoutReference.Height, dpiScale);
}
