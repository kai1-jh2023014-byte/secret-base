using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using SecretBase.Core.Themes;

namespace SecretBase.Widgets.Theming;

/// <summary>
/// Shared Secret Base visual language — Atelier surfaces, calm hierarchy, subtle motion.
/// </summary>
public static class WidgetSurfaceStyle
{
    public static void ApplyChrome(Border border, ThemeDefinition theme, bool elevated = true)
    {
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(theme);

        var opacity = ThemePainter.SoftSurfaceOpacity(theme);
        border.Background = ThemePainter.Brush(theme.WidgetBackground, opacity);
        border.CornerRadius = new CornerRadius(theme.CornerRadius);
        border.BorderThickness = new Thickness(elevated ? 1 : 0.5);
        border.BorderBrush = ThemePainter.Brush(theme.Border, elevated ? 0.35 : 0.22);
        var pad = Math.Max(10, theme.Spacing + 4);
        border.Padding = new Thickness(pad, pad - 2, pad, pad - 2);
    }

    /// <summary>Layered surface: outer wash + inner card for depth without ThemeShadow.</summary>
    public static void ApplyLayeredChrome(Border outer, Border inner, ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(theme);

        outer.Background = ThemePainter.Brush(
            string.IsNullOrWhiteSpace(theme.SurfaceElevated) ? theme.SurfaceSecondary : theme.SurfaceElevated,
            ThemePainter.SoftSurfaceOpacity(theme) * 0.45);
        outer.CornerRadius = new CornerRadius(theme.CornerRadius + 2);
        outer.BorderThickness = new Thickness(1);
        outer.BorderBrush = ThemePainter.Brush(theme.Border, 0.18);
        outer.Padding = new Thickness(2);

        ApplyChrome(inner, theme, elevated: true);
        inner.CornerRadius = new CornerRadius(Math.Max(8, theme.CornerRadius - 2));
    }

    public static void ApplyHeader(TextBlock header, TextBlock? subtitle, ThemeDefinition theme)
    {
        var font = new FontFamily(theme.FontFamily);
        header.FontFamily = font;
        header.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        header.CharacterSpacing = 40;
        if (subtitle is null)
        {
            return;
        }

        subtitle.FontFamily = font;
        subtitle.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        subtitle.CharacterSpacing = 20;
    }

    public static void ApplyMuted(TextBlock label, ThemeDefinition theme)
    {
        label.FontFamily = new FontFamily(theme.FontFamily);
        label.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
    }

    public static void ApplyBody(TextBlock body, ThemeDefinition theme)
    {
        body.FontFamily = new FontFamily(theme.FontFamily);
        body.Foreground = ThemePainter.Brush(theme.WidgetForeground);
    }

    public static void ApplyActionButton(Button button, ThemeDefinition theme, bool accent = false)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = accent
            ? ThemePainter.Brush(theme.Accent, 0.88)
            : ThemePainter.Brush(theme.SurfaceSecondary, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Border, accent ? 0.15 : 0.45);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(Math.Max(8, theme.CornerRadius * 0.45));
        button.Padding = new Thickness(12, 6, 12, 6);
    }

    public static void ApplyGhostButton(Button button, ThemeDefinition theme)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = ThemePainter.Brush(theme.WidgetBackground, 0.15);
        button.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        button.BorderBrush = ThemePainter.Brush(theme.Border, 0.35);
        button.BorderThickness = new Thickness(1);
        button.CornerRadius = new CornerRadius(Math.Max(8, theme.CornerRadius * 0.45));
    }

    /// <summary>Thin glass pill for floating shelf / icon strips.</summary>
    public static void ApplyFloatingPill(Border border, ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(border);
        ArgumentNullException.ThrowIfNull(theme);

        var opacity = Math.Clamp(ThemePainter.SoftSurfaceOpacity(theme), 0.18, 0.48);
        border.Background = ThemePainter.Brush(theme.WidgetBackground, opacity);
        border.BorderBrush = ThemePainter.Brush(theme.Border, 0.22);
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(Math.Max(18, theme.CornerRadius + 4));
        border.Padding = new Thickness(14, 6, 14, 6);
    }

    /// <summary>Quiet icon/control button without heavy chrome.</summary>
    public static void ApplyIconButton(Button button, ThemeDefinition theme, bool accent = false)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = accent
            ? ThemePainter.Brush(theme.Accent, 0.55)
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground, accent ? 1 : 0.9);
        button.BorderBrush = ThemePainter.Brush(theme.Border, accent ? 0.18 : 0);
        button.BorderThickness = new Thickness(accent ? 1 : 0);
        button.CornerRadius = new CornerRadius(Math.Max(14, theme.CornerRadius * 0.7));
        button.Padding = new Thickness(8, 4, 8, 4);
    }

    public static void ApplyProgress(ProgressBar bar, ThemeDefinition theme)
    {
        bar.Foreground = ThemePainter.Brush(theme.Accent, 0.95);
        bar.Background = ThemePainter.Brush(theme.Border, 0.25);
        bar.Height = 3;
        bar.CornerRadius = new CornerRadius(2);
    }

    public static void FadeOpacity(UIElement target, double to, double milliseconds = 180)
    {
        var duration = milliseconds <= 0 ? 180 : milliseconds;
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(duration),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var board = new Storyboard();
        board.Children.Add(animation);
        board.Begin();
    }

    public static void PulseScale(UIElement target)
    {
        if (target.RenderTransform is not ScaleTransform)
        {
            target.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            target.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        }

        var up = new DoubleAnimation
        {
            To = 1.015,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(up, target.RenderTransform);
        Storyboard.SetTargetProperty(up, "ScaleX");
        var upY = new DoubleAnimation
        {
            To = 1.015,
            Duration = TimeSpan.FromMilliseconds(120),
            AutoReverse = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(upY, target.RenderTransform);
        Storyboard.SetTargetProperty(upY, "ScaleY");
        var board = new Storyboard();
        board.Children.Add(up);
        board.Children.Add(upY);
        board.Begin();
    }
}
