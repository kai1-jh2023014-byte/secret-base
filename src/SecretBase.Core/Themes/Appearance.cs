namespace SecretBase.Core.Themes;

public static class AppearanceDensity
{
    public const string Compact = "Compact";
    public const string Comfortable = "Comfortable";
    public const string Spacious = "Spacious";

    public static IReadOnlyList<string> All { get; } = [Compact, Comfortable, Spacious];
}

public static class AppearanceShape
{
    public const string Soft = "Soft";
    public const string Balanced = "Balanced";
    public const string Sharp = "Sharp";

    public static IReadOnlyList<string> All { get; } = [Soft, Balanced, Sharp];
}

public static class AppearanceMotion
{
    public const string Subtle = "Subtle";
    public const string Standard = "Standard";
    public const string Reduced = "Reduced";

    public static IReadOnlyList<string> All { get; } = [Subtle, Standard, Reduced];
}

public static class VisualThemeNames
{
    public const string Atelier = "Atelier";
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string Midnight = "Midnight";
    public const string Soft = "Soft";
    public const string Minimal = "Minimal";
    public const string Glass = "Glass";
    public const string HighContrast = "High Contrast";

    /// <summary>Primary gallery shown in Appearance. Other presets remain available.</summary>
    public static IReadOnlyList<string> Gallery { get; } =
        [Atelier, Light, Dark, Midnight, Soft, Minimal, Glass, HighContrast];
}

/// <summary>Explicit UI preference only — never inferred personality.</summary>
public sealed class AppearanceProfile
{
    public string VisualTheme { get; set; } = VisualThemeNames.Atelier;

    public string AccentName { get; set; } = AccentPalette.Jade;

    public string? CustomAccent { get; set; }

    public string Density { get; set; } = AppearanceDensity.Comfortable;

    public string Shape { get; set; } = AppearanceShape.Balanced;

    public string Motion { get; set; } = AppearanceMotion.Subtle;

    public double? Transparency { get; set; }

    public static AppearanceProfile From(ThemeDefinition theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return new AppearanceProfile
        {
            VisualTheme = string.IsNullOrWhiteSpace(theme.DisplayName) ? VisualThemeNames.Atelier : theme.DisplayName,
            AccentName = string.IsNullOrWhiteSpace(theme.AccentName) ? AccentPalette.Jade : theme.AccentName,
            CustomAccent = theme.Accent,
            Density = string.IsNullOrWhiteSpace(theme.Density) ? AppearanceDensity.Comfortable : theme.Density,
            Shape = string.IsNullOrWhiteSpace(theme.Shape) ? AppearanceShape.Balanced : theme.Shape,
            Motion = theme.ReducedMotion ? AppearanceMotion.Reduced
                : string.IsNullOrWhiteSpace(theme.Motion) ? AppearanceMotion.Subtle : theme.Motion,
            Transparency = theme.Transparency
        };
    }

    public string Format() =>
        string.Join(
            " · ",
            new[] { VisualTheme, Density, Shape, AccentName + " accent", Motion + " motion" }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>Composes visual theme + accent + density + shape + motion into one token set.</summary>
public static class AppearanceComposer
{
    public static void Apply(ThemeDefinition theme, AppearanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(profile);

        ThemePresets.ApplyPreset(theme, profile.VisualTheme);
        ApplyAccent(theme, profile.AccentName, profile.CustomAccent);
        ApplyDensity(theme, profile.Density);
        ApplyShape(theme, profile.Shape);
        ApplyMotion(theme, profile.Motion);
        if (profile.Transparency is double transparency)
        {
            theme.Transparency = Math.Clamp(transparency, 0.35, 1.0);
        }

        theme.DisplayName = string.IsNullOrWhiteSpace(profile.VisualTheme)
            ? VisualThemeNames.Atelier
            : profile.VisualTheme.Trim();
        ThemeMigrator.Normalize(theme);
    }

    public static void ApplyAccent(ThemeDefinition theme, string? accentName, string? customHex = null)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var name = string.IsNullOrWhiteSpace(accentName) ? AccentPalette.Jade : accentName.Trim();
        theme.AccentName = name;
        if (string.Equals(name, AccentPalette.Custom, StringComparison.OrdinalIgnoreCase)
            && ThemeColor.TryParse(customHex, out _, out _, out _, out _))
        {
            theme.Accent = customHex!.StartsWith('#') ? customHex : "#" + customHex;
        }
        else if (AccentPalette.TryGet(name, out var accent))
        {
            theme.Accent = accent;
        }

        theme.AccentSecondary = AccentPalette.Secondary(theme.Accent);
        theme.FocusRing = theme.Accent;
        theme.OnAccent = ThemeColor.ContrastOn(theme.Accent);
    }

    public static void ApplyDensity(ThemeDefinition theme, string? density)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var value = density?.Trim() ?? AppearanceDensity.Comfortable;
        theme.Density = value;
        switch (value)
        {
            case AppearanceDensity.Compact:
                theme.Spacing = 6;
                theme.TitleSize = 36;
                theme.BodySize = 12;
                theme.CaptionSize = 10;
                theme.WidgetMinWidth = 180;
                theme.WidgetMinHeight = 108;
                break;
            case AppearanceDensity.Spacious:
                theme.Spacing = 14;
                theme.TitleSize = 48;
                theme.BodySize = 15;
                theme.CaptionSize = 12;
                theme.WidgetMinWidth = 220;
                theme.WidgetMinHeight = 140;
                break;
            default:
                theme.Density = AppearanceDensity.Comfortable;
                theme.Spacing = 10;
                theme.TitleSize = 44;
                theme.BodySize = 13;
                theme.CaptionSize = 11;
                theme.WidgetMinWidth = 200;
                theme.WidgetMinHeight = 120;
                break;
        }
    }

    public static void ApplyShape(ThemeDefinition theme, string? shape)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var value = shape?.Trim() ?? AppearanceShape.Balanced;
        theme.Shape = value;
        theme.CornerRadius = value switch
        {
            AppearanceShape.Soft => 22,
            AppearanceShape.Sharp => 6,
            _ => 18
        };
        if (value is not AppearanceShape.Soft and not AppearanceShape.Sharp)
        {
            theme.Shape = AppearanceShape.Balanced;
        }
    }

    public static void ApplyMotion(ThemeDefinition theme, string? motion)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var value = motion?.Trim() ?? AppearanceMotion.Subtle;
        theme.Motion = value;
        switch (value)
        {
            case AppearanceMotion.Reduced:
                theme.ReducedMotion = true;
                theme.MotionDurationMs = 0;
                break;
            case AppearanceMotion.Standard:
                theme.ReducedMotion = false;
                theme.MotionDurationMs = 220;
                break;
            default:
                theme.Motion = AppearanceMotion.Subtle;
                theme.ReducedMotion = false;
                theme.MotionDurationMs = 160;
                break;
        }
    }
}
