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
            return Color.FromArgb(255, 18, 22, 30);
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
            return Color.FromArgb(255, 18, 22, 30);
        }

        var a = Convert.ToByte(value[..2], 16);
        var r = Convert.ToByte(value[2..4], 16);
        var g = Convert.ToByte(value[4..6], 16);
        var b = Convert.ToByte(value[6..8], 16);
        return Color.FromArgb(a, r, g, b);
    }

    public static double EffectiveWidgetOpacity(ThemeDefinition theme) =>
        Math.Clamp(theme.Transparency, 0.35, 1.0);

    /// <summary>Blend two hex colors by <paramref name="amount"/> toward <paramref name="toHex"/>.</summary>
    public static Color Blend(string fromHex, string toHex, double amount)
    {
        var from = ParseColor(fromHex);
        var to = ParseColor(toHex);
        amount = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            (byte)Math.Round(from.A + (to.A - from.A) * amount),
            (byte)Math.Round(from.R + (to.R - from.R) * amount),
            (byte)Math.Round(from.G + (to.G - from.G) * amount),
            (byte)Math.Round(from.B + (to.B - from.B) * amount));
    }

    public static SolidColorBrush BlendBrush(string fromHex, string toHex, double amount, double? opacityOverride = null)
    {
        var color = Blend(fromHex, toHex, amount);
        if (opacityOverride is double opacity)
        {
            color.A = (byte)Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        }

        return new SolidColorBrush(color);
    }
}
