using Windows.UI;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Themes;

namespace SecretBase.Widgets.Theming;

/// <summary>
/// Maps ThemeDefinition tokens to WinUI brushes/values. Keeps hex parsing out of individual widgets.
/// </summary>
public static class ThemePainter
{
    public static SolidColorBrush Brush(string hex, double? opacityOverride = null)
    {
        var color = ParseColor(hex);
        if (opacityOverride is double opacity)
        {
            color.A = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        }

        return new SolidColorBrush(color);
    }

    public static Color ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Color.FromArgb(255, 26, 35, 50);
        }

        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        if (value.Length != 8)
        {
            return Color.FromArgb(255, 26, 35, 50);
        }

        var a = Convert.ToByte(value[..2], 16);
        var r = Convert.ToByte(value[2..4], 16);
        var g = Convert.ToByte(value[4..6], 16);
        var b = Convert.ToByte(value[6..8], 16);
        return Color.FromArgb(a, r, g, b);
    }

    public static double EffectiveWidgetOpacity(ThemeDefinition theme) =>
        Math.Clamp(theme.Transparency, 0.35, 1.0);
}
