namespace SecretBase.Core.Themes;

/// <summary>
/// Built-in visual presets for Secret Base surfaces (Atelier language).
/// Mutates an existing <see cref="ThemeDefinition"/> in place (same Id).
/// </summary>
public static class ThemePresets
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Atelier",
        "Minimal",
        "Glass",
        "Dark",
        "Light",
        "Focus",
        "Aurora",
        "Mono",
        "Midnight",
        "Warm Paper",
        "Forest",
        "Ocean",
        "Soft Rose"
    ];

    public static void ApplyPreset(ThemeDefinition theme, string presetName)
    {
        ArgumentNullException.ThrowIfNull(theme);
        switch (presetName.Trim())
        {
            case "Minimal":
                Apply(
                    theme,
                    background: "#FF101318",
                    backgroundSecondary: "#FF171B22",
                    foreground: "#FFF2F4F7",
                    foregroundMuted: "#FF8A909B",
                    accent: "#FF8FA3C4",
                    widgetBackground: "#E612161C",
                    widgetForeground: "#FFF2F4F7",
                    surfaceSecondary: "#30171B22",
                    border: "#36FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 12,
                    transparency: 0.92,
                    spacing: 8);
                break;
            case "Glass":
                Apply(
                    theme,
                    background: "#FF0C1420",
                    backgroundSecondary: "#FF152236",
                    foreground: "#FFEAF3FF",
                    foregroundMuted: "#FF90A8C0",
                    accent: "#FF6BB8D6",
                    widgetBackground: "#55101828",
                    widgetForeground: "#FFF2F8FF",
                    surfaceSecondary: "#28101828",
                    border: "#55FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 22,
                    transparency: 0.7,
                    spacing: 12,
                    blur: 14);
                break;
            case "Dark":
                Apply(
                    theme,
                    background: "#FF08090C",
                    backgroundSecondary: "#FF12141A",
                    foreground: "#FFECEFF4",
                    foregroundMuted: "#FF7A8190",
                    accent: "#FF7A9E86",
                    widgetBackground: "#CC12141A",
                    widgetForeground: "#FFECEFF4",
                    surfaceSecondary: "#3012141A",
                    border: "#30FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 14,
                    transparency: 0.9,
                    spacing: 10);
                break;
            case "Light":
                Apply(
                    theme,
                    background: "#FFF4F6F8",
                    backgroundSecondary: "#FFE8ECF1",
                    foreground: "#FF1A2330",
                    foregroundMuted: "#FF5C6775",
                    accent: "#FF3F6F58",
                    widgetBackground: "#F5FFFFFF",
                    widgetForeground: "#FF1A2330",
                    surfaceSecondary: "#E6F0F3F6",
                    border: "#33000000",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 16,
                    transparency: 0.96,
                    spacing: 10);
                break;
            case "Focus":
                Apply(
                    theme,
                    background: "#FF0F1218",
                    backgroundSecondary: "#FF161B24",
                    foreground: "#FFE8EDF5",
                    foregroundMuted: "#FF7A8494",
                    accent: "#FF8FA87A",
                    widgetBackground: "#CC161B24",
                    widgetForeground: "#FFE8EDF5",
                    surfaceSecondary: "#30161B24",
                    border: "#2EFFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 10,
                    transparency: 0.9,
                    spacing: 8);
                break;
            case "Aurora":
                Apply(
                    theme,
                    background: "#FF0A1220",
                    backgroundSecondary: "#FF132438",
                    foreground: "#FFE4F6FF",
                    foregroundMuted: "#FF7FA4B8",
                    accent: "#FF6FD4C8",
                    widgetBackground: "#CC132033",
                    widgetForeground: "#FFE4F6FF",
                    surfaceSecondary: "#28132033",
                    border: "#40FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 18,
                    transparency: 0.86,
                    spacing: 10);
                break;
            case "Mono":
                Apply(
                    theme,
                    background: "#FF0F0F0F",
                    backgroundSecondary: "#FF1A1A1A",
                    foreground: "#FFF0F0F0",
                    foregroundMuted: "#FF9A9A9A",
                    accent: "#FFB8B8B8",
                    widgetBackground: "#CC1A1A1A",
                    widgetForeground: "#FFF0F0F0",
                    surfaceSecondary: "#301A1A1A",
                    border: "#38FFFFFF",
                    fontFamily: "Cascadia Mono",
                    cornerRadius: 8,
                    transparency: 0.92,
                    spacing: 8);
                break;
            case "Midnight":
                Apply(
                    theme,
                    background: "#FF0B1020",
                    backgroundSecondary: "#FF161B2E",
                    foreground: "#FFE8ECF4",
                    foregroundMuted: "#FF8B93A7",
                    accent: "#FF6C8CFF",
                    widgetBackground: "#CC141A2C",
                    widgetForeground: "#FFF2F5FF",
                    surfaceSecondary: "#28141A2C",
                    border: "#40FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 14,
                    transparency: 0.9,
                    spacing: 10);
                break;
            case "Warm Paper":
                Apply(
                    theme,
                    background: "#FFF2E9DA",
                    backgroundSecondary: "#FFE4D4C0",
                    foreground: "#FF2A2118",
                    foregroundMuted: "#FF7A6554",
                    accent: "#FFB06A3C",
                    widgetBackground: "#F0FFF8EE",
                    widgetForeground: "#FF2A2118",
                    surfaceSecondary: "#E6F5EADF",
                    border: "#33000000",
                    fontFamily: "Georgia",
                    cornerRadius: 12,
                    transparency: 0.94,
                    spacing: 10);
                break;
            case "Forest":
                Apply(
                    theme,
                    background: "#FF101A14",
                    backgroundSecondary: "#FF1A2B22",
                    foreground: "#FFE7F2E9",
                    foregroundMuted: "#FF8FA896",
                    accent: "#FF6B9F7A",
                    widgetBackground: "#CC1A2B22",
                    widgetForeground: "#FFEAF6EE",
                    surfaceSecondary: "#281A2B22",
                    border: "#36FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 18,
                    transparency: 0.88,
                    spacing: 10);
                break;
            case "Ocean":
                Apply(
                    theme,
                    background: "#FF0C1720",
                    backgroundSecondary: "#FF143044",
                    foreground: "#FFE4F3FA",
                    foregroundMuted: "#FF7FA0B3",
                    accent: "#FF3DA9C8",
                    widgetBackground: "#CC132433",
                    widgetForeground: "#FFEAF7FC",
                    surfaceSecondary: "#28132433",
                    border: "#3AFFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 16,
                    transparency: 0.87,
                    spacing: 10);
                break;
            case "Soft Rose":
                Apply(
                    theme,
                    background: "#FF241820",
                    backgroundSecondary: "#FF372631",
                    foreground: "#FFF8ECEF",
                    foregroundMuted: "#FFB897A2",
                    accent: "#FFD4849A",
                    widgetBackground: "#CCD9B4C0",
                    widgetForeground: "#FF2A1C24",
                    surfaceSecondary: "#40D9B4C0",
                    border: "#40FFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 20,
                    transparency: 0.9,
                    spacing: 10);
                break;
            default:
                // Atelier — match ThemeDefinition.CreateDefault()
                Apply(
                    theme,
                    background: "#FF0E1218",
                    backgroundSecondary: "#FF171D27",
                    foreground: "#FFF3EFE6",
                    foregroundMuted: "#FF8B93A0",
                    accent: "#FF7A9E86",
                    widgetBackground: "#D9181E28",
                    widgetForeground: "#FFF3EFE6",
                    surfaceSecondary: "#40151A24",
                    border: "#3DFFFFFF",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 18,
                    transparency: 0.88,
                    spacing: 10);
                break;
        }

        theme.DisplayName = string.IsNullOrWhiteSpace(presetName) || presetName.Trim() is "Default" or "Atelier"
            ? "Atelier"
            : presetName.Trim();
    }

    public static void CopyVisualsTo(ThemeDefinition source, ThemeDefinition target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        Apply(
            target,
            source.Background,
            source.BackgroundSecondary,
            source.Foreground,
            source.ForegroundMuted,
            source.Accent,
            source.WidgetBackground,
            source.WidgetForeground,
            source.SurfaceSecondary,
            source.Border,
            source.FontFamily,
            source.CornerRadius,
            source.Transparency,
            source.Spacing,
            source.BlurAmount);
        target.ShadowOpacity = source.ShadowOpacity;
        target.WidgetMinWidth = source.WidgetMinWidth;
        target.WidgetMinHeight = source.WidgetMinHeight;
        target.DisplayName = source.DisplayName;
    }

    private static void Apply(
        ThemeDefinition theme,
        string background,
        string backgroundSecondary,
        string foreground,
        string foregroundMuted,
        string accent,
        string widgetBackground,
        string widgetForeground,
        string surfaceSecondary,
        string border,
        string fontFamily,
        double cornerRadius,
        double transparency,
        double spacing,
        double blur = 0)
    {
        theme.Background = background;
        theme.BackgroundSecondary = backgroundSecondary;
        theme.Foreground = foreground;
        theme.ForegroundMuted = foregroundMuted;
        theme.Accent = accent;
        theme.WidgetBackground = widgetBackground;
        theme.WidgetForeground = widgetForeground;
        theme.SurfaceSecondary = surfaceSecondary;
        theme.Border = border;
        theme.FontFamily = fontFamily;
        theme.CornerRadius = cornerRadius;
        theme.Transparency = Math.Clamp(transparency, 0.35, 1.0);
        theme.Spacing = spacing;
        theme.BlurAmount = blur;
    }
}
