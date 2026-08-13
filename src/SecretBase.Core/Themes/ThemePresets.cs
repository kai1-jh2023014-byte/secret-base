namespace SecretBase.Core.Themes;

/// <summary>
/// Built-in visual presets for Clock / Text / Block surfaces.
/// Mutates an existing <see cref="ThemeDefinition"/> in place (same Id).
/// </summary>
public static class ThemePresets
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "Default",
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
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 14,
                    transparency: 0.9);
                break;
            case "Warm Paper":
                Apply(
                    theme,
                    background: "#FFF4ECDF",
                    backgroundSecondary: "#FFE6D5BC",
                    foreground: "#FF2C2118",
                    foregroundMuted: "#FF7A6554",
                    accent: "#FFC4713B",
                    widgetBackground: "#E6FFF8EE",
                    widgetForeground: "#FF2C2118",
                    fontFamily: "Georgia",
                    cornerRadius: 10,
                    transparency: 0.92);
                break;
            case "Forest":
                Apply(
                    theme,
                    background: "#FF122018",
                    backgroundSecondary: "#FF1D3326",
                    foreground: "#FFE7F2E9",
                    foregroundMuted: "#FF8FA896",
                    accent: "#FF6B9F7A",
                    widgetBackground: "#CC1A2B22",
                    widgetForeground: "#FFEAF6EE",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 18,
                    transparency: 0.88);
                break;
            case "Ocean":
                Apply(
                    theme,
                    background: "#FF0E1A24",
                    backgroundSecondary: "#FF163246",
                    foreground: "#FFE4F3FA",
                    foregroundMuted: "#FF7FA0B3",
                    accent: "#FF3DA9C8",
                    widgetBackground: "#CC132433",
                    widgetForeground: "#FFEAF7FC",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 16,
                    transparency: 0.87);
                break;
            case "Soft Rose":
                Apply(
                    theme,
                    background: "#FF2A1C24",
                    backgroundSecondary: "#FF3C2733",
                    foreground: "#FFF8ECEF",
                    foregroundMuted: "#FFB897A2",
                    accent: "#FFD4849A",
                    widgetBackground: "#CCD9B4C0",
                    widgetForeground: "#FF2A1C24",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 20,
                    transparency: 0.9);
                break;
            default:
                // Default — match ThemeDefinition.CreateDefault() tokens.
                Apply(
                    theme,
                    background: "#FF1A2332",
                    backgroundSecondary: "#FF2F4A3C",
                    foreground: "#FFF4F0E6",
                    foregroundMuted: "#FF9AA7B2",
                    accent: "#FF6B9F7A",
                    widgetBackground: "#CC243447",
                    widgetForeground: "#FFF4F0E6",
                    fontFamily: "Segoe UI Variable Display",
                    cornerRadius: 16,
                    transparency: 0.85);
                break;
        }

        theme.DisplayName = string.IsNullOrWhiteSpace(presetName) ? "Default" : presetName.Trim();
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
            source.FontFamily,
            source.CornerRadius,
            source.Transparency);
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
        string fontFamily,
        double cornerRadius,
        double transparency)
    {
        theme.Background = background;
        theme.BackgroundSecondary = backgroundSecondary;
        theme.Foreground = foreground;
        theme.ForegroundMuted = foregroundMuted;
        theme.Accent = accent;
        theme.WidgetBackground = widgetBackground;
        theme.WidgetForeground = widgetForeground;
        theme.FontFamily = fontFamily;
        theme.CornerRadius = cornerRadius;
        theme.Transparency = Math.Clamp(transparency, 0.35, 1.0);
    }
}
