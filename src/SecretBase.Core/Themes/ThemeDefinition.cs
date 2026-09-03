namespace SecretBase.Core.Themes;

/// <summary>
/// Theme tokens consumed by Desktop/Widgets. Values are stored as strings so JSON stays simple.
/// Colors use #AARRGGBB or #RRGGBB.
/// Default visual language: Atelier — deep slate, warm ivory, restrained jade.
/// </summary>
public sealed class ThemeDefinition
{
    public int SchemaVersion { get; set; } = ThemeMigrator.CurrentSchema;

    public string Id { get; set; } = "default";

    public string DisplayName { get; set; } = "Atelier";

    public string Background { get; set; } = "#FF0E1218";

    public string BackgroundSecondary { get; set; } = "#FF171D27";

    public string Foreground { get; set; } = "#FFF3EFE6";

    public string ForegroundMuted { get; set; } = "#FF8B93A0";

    public string Accent { get; set; } = "#FF7A9E86";

    public string AccentSecondary { get; set; } = "#FF5E7A68";

    /// <summary>Text drawn on an accent fill (buttons).</summary>
    public string OnAccent { get; set; } = "#FFF6F3EC";

    public string WidgetBackground { get; set; } = "#D9181E28";

    public string WidgetForeground { get; set; } = "#FFF3EFE6";

    public string SurfaceSecondary { get; set; } = "#40151A24";

    /// <summary>Raised glass over <see cref="WidgetBackground"/>.</summary>
    public string SurfaceElevated { get; set; } = "#E01E2530";

    public string Border { get; set; } = "#3DFFFFFF";

    public string FocusRing { get; set; } = "#FF7A9E86";

    public string StatusSuccess { get; set; } = "#FF7A9E86";

    public string StatusWarning { get; set; } = "#FFC4A35A";

    public string StatusError { get; set; } = "#FFC47A72";

    public double ShadowOpacity { get; set; } = 0.22;

    public double BlurAmount { get; set; } = 0;

    public double Spacing { get; set; } = 10;

    public double TitleSize { get; set; } = 44;

    public double BodySize { get; set; } = 13;

    public double CaptionSize { get; set; } = 11;

    /// <summary>Shared fade / emphasis duration in milliseconds. 0 when reduced motion.</summary>
    public double MotionDurationMs { get; set; } = 160;

    public string FontFamily { get; set; } = "Segoe UI Variable Display";

    public double CornerRadius { get; set; } = 18;

    /// <summary>0..1 overall widget surface opacity hint.</summary>
    public double Transparency { get; set; } = 0.88;

    public double WidgetMinWidth { get; set; } = 200;

    public double WidgetMinHeight { get; set; } = 120;

    public string Density { get; set; } = AppearanceDensity.Comfortable;

    public string Shape { get; set; } = AppearanceShape.Balanced;

    public string Motion { get; set; } = AppearanceMotion.Subtle;

    public string AccentName { get; set; } = AccentPalette.Jade;

    public bool ReducedMotion { get; set; }

    public static ThemeDefinition CreateDefault() => ThemeMigrator.Normalize(new ThemeDefinition());
}
