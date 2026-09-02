using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Themes;

namespace SecretBase.Widgets.Theming;

/// <summary>Shared widget surface styling from design tokens.</summary>
public static class WidgetSurfaceStyle
{
    public static void ApplyChrome(Border border, ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(theme);
        border.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        border.CornerRadius = new CornerRadius(theme.CornerRadius);
        border.BorderBrush = ThemePainter.Brush(theme.Border, 0.9);
        border.BorderThickness = new Thickness(1);
        border.Padding = new Thickness(theme.Spacing + 4, theme.Spacing + 2, theme.Spacing + 4, theme.Spacing + 2);
    }

    public static void ApplyHeader(TextBlock header, TextBlock? subtitle, ThemeDefinition theme)
    {
        var font = new FontFamily(theme.FontFamily);
        header.FontFamily = font;
        header.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        if (subtitle is null)
        {
            return;
        }

        subtitle.FontFamily = font;
        subtitle.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
    }

    public static void ApplyActionButton(Button button, ThemeDefinition theme, bool accent = false)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = accent
            ? ThemePainter.Brush(theme.Accent, 0.85)
            : ThemePainter.Brush(theme.SurfaceSecondary, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Border, 0.75);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(Math.Max(6, theme.CornerRadius * 0.5));
    }
}
