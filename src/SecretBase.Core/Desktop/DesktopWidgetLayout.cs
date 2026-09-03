using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Desktop;

/// <summary>
/// Neat grid placement for Desktop widgets (Clock, Text, …).
/// Uses consistent gaps from the top-left (compact), not stretched full-bleed cells.
/// </summary>
public static class DesktopWidgetLayout
{
    public const string BandLarge = "Large";
    public const string BandMedium = "Medium";
    public const string BandSmall = "Small";
    public const string BandOverlay = "Overlay";

    /// <summary>Adaptive margin/gap from work-area width and density. Does not change widget sizes.</summary>
    public static (double Margin, double Gap, string Band) MetricsFor(double areaWidth, string? density = null)
    {
        var band = areaWidth >= 1400 ? BandLarge
            : areaWidth >= 960 ? BandMedium
            : areaWidth >= 640 ? BandSmall
            : BandOverlay;
        var compact = string.Equals(density, AppearanceDensity.Compact, StringComparison.OrdinalIgnoreCase);
        var spacious = string.Equals(density, AppearanceDensity.Spacious, StringComparison.OrdinalIgnoreCase);
        var margin = band switch
        {
            BandLarge => spacious ? 32 : compact ? 16 : 24,
            BandMedium => spacious ? 24 : compact ? 14 : 20,
            BandSmall => spacious ? 18 : compact ? 10 : 16,
            _ => spacious ? 14 : compact ? 8 : 12
        };
        return (margin, margin, band);
    }

    public static void ArrangeEvenly(
        IReadOnlyList<WidgetInstance> widgets,
        double areaWidth,
        double areaHeight,
        double margin = 24,
        double gap = 24)
    {
        if (widgets.Count == 0)
        {
            return;
        }

        var width = Math.Max(120, areaWidth);
        var height = Math.Max(80, areaHeight);
        var pad = Math.Max(8, margin);
        var spacing = Math.Max(8, gap);

        var cellW = widgets.Max(w => Math.Max(120, w.Size.Width));
        var cellH = widgets.Max(w => Math.Max(80, w.Size.Height));
        var usableW = Math.Max(cellW, width - pad * 2);

        var maxCols = Math.Max(1, (int)Math.Floor((usableW + spacing) / (cellW + spacing)));
        var cols = Math.Min(maxCols, widgets.Count);

        for (var i = 0; i < widgets.Count; i++)
        {
            var widget = widgets[i];
            var col = i % cols;
            var row = i / cols;
            // Center each widget inside its equal cell for a tidy look.
            var cellLeft = pad + col * (cellW + spacing);
            var cellTop = pad + row * (cellH + spacing);
            var x = cellLeft + Math.Max(0, (cellW - widget.Size.Width) / 2);
            var y = cellTop + Math.Max(0, (cellH - widget.Size.Height) / 2);
            widget.Position.X = Math.Clamp(x, 0, Math.Max(0, width - widget.Size.Width));
            widget.Position.Y = Math.Clamp(y, 0, Math.Max(0, height - widget.Size.Height));
        }
    }
}
