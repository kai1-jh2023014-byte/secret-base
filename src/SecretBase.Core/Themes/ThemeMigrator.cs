namespace SecretBase.Core.Themes;

/// <summary>Fills new appearance tokens on themes saved before schema 2.</summary>
public static class ThemeMigrator
{
    public const int CurrentSchema = 2;

    public static ThemeDefinition Normalize(ThemeDefinition? theme)
    {
        theme ??= new ThemeDefinition();
        if (theme.SchemaVersion < 1)
        {
            theme.SchemaVersion = 1;
        }

        if (string.IsNullOrWhiteSpace(theme.DisplayName))
        {
            theme.DisplayName = VisualThemeNames.Atelier;
        }

        if (string.IsNullOrWhiteSpace(theme.SurfaceElevated))
        {
            theme.SurfaceElevated = theme.WidgetBackground;
        }

        if (theme.MotionDurationMs < 0)
        {
            theme.MotionDurationMs = 0;
        }

        if (theme.TitleSize <= 0)
        {
            theme.TitleSize = 44;
        }

        if (theme.BodySize <= 0)
        {
            theme.BodySize = 13;
        }

        if (theme.CaptionSize <= 0)
        {
            theme.CaptionSize = 11;
        }

        if (string.IsNullOrWhiteSpace(theme.StatusSuccess))
        {
            theme.StatusSuccess = "#FF7A9E86";
        }

        if (string.IsNullOrWhiteSpace(theme.StatusWarning))
        {
            theme.StatusWarning = "#FFC4A35A";
        }

        if (string.IsNullOrWhiteSpace(theme.StatusError))
        {
            theme.StatusError = "#FFC47A72";
        }

        if (string.IsNullOrWhiteSpace(theme.AccentSecondary))
        {
            theme.AccentSecondary = AccentPalette.Secondary(theme.Accent);
        }

        if (string.IsNullOrWhiteSpace(theme.OnAccent))
        {
            theme.OnAccent = ThemeColor.ContrastOn(theme.Accent);
        }

        if (string.IsNullOrWhiteSpace(theme.FocusRing))
        {
            theme.FocusRing = theme.Accent;
        }

        if (string.IsNullOrWhiteSpace(theme.Density))
        {
            theme.Density = AppearanceDensity.Comfortable;
        }

        if (string.IsNullOrWhiteSpace(theme.Shape))
        {
            theme.Shape = theme.CornerRadius switch
            {
                >= 20 => AppearanceShape.Soft,
                <= 8 => AppearanceShape.Sharp,
                _ => AppearanceShape.Balanced
            };
        }

        if (string.IsNullOrWhiteSpace(theme.Motion))
        {
            theme.Motion = theme.ReducedMotion ? AppearanceMotion.Reduced : AppearanceMotion.Subtle;
        }

        if (string.IsNullOrWhiteSpace(theme.AccentName)
            || !AccentPalette.HexMatchesName(theme.AccentName, theme.Accent))
        {
            theme.AccentName = AccentPalette.MatchName(theme.Accent);
        }

        theme.Transparency = Math.Clamp(theme.Transparency, 0.35, 1.0);
        theme.CornerRadius = Math.Clamp(theme.CornerRadius, 0, 40);
        theme.Spacing = Math.Clamp(theme.Spacing, 4, 24);
        theme.SchemaVersion = CurrentSchema;
        return theme;
    }
}
