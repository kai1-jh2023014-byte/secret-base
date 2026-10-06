namespace SecretBase.Core.Desktop;

/// <summary>
/// Canonical design canvas for Secret Base desktop objects.
/// Existing 1920×1080 placements are the visual source of truth; other viewports
/// are derived via <see cref="ResponsiveLayoutResolver"/> without uniform stretch.
/// </summary>
public static class DesktopLayoutReference
{
    public const double Width = 1920;
    public const double Height = 1080;

    public const double Margin = DesktopViewportLayout.DefaultMargin;
    public const double BottomReserve = DesktopViewportLayout.DefaultBottomReserve;

    public static bool Matches(double width, double height, double tolerance = 8) =>
        Math.Abs(width - Width) <= tolerance && Math.Abs(height - Height) <= tolerance;
}
