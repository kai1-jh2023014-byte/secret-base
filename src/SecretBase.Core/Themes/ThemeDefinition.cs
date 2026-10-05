namespace SecretBase.Core.Themes;

/// <summary>
/// Theme tokens consumed by Desktop/Widgets. Values are stored as strings so JSON stays simple.
/// Colors use #AARRGGBB or #RRGGBB.
/// Default visual language: Atelier — deep slate, warm ivory, restrained jade.
/// </summary>
public sealed class ThemeDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public string Id { get; set; } = "default";

    public string DisplayName { get; set; } = "Atelier";

    public string Background { get; set; } = "#FF0E1218";

    public string BackgroundSecondary { get; set; } = "#FF171D27";

    public string Foreground { get; set; } = "#FFF3EFE6";

    public string ForegroundMuted { get; set; } = "#FF8B93A0";

    public string Accent { get; set; } = "#FF7A9E86";

    public string WidgetBackground { get; set; } = "#66181E28";

    public string WidgetForeground { get; set; } = "#FFF3EFE6";

    public string SurfaceSecondary { get; set; } = "#28151A24";

    /// <summary>Raised glass over <see cref="WidgetBackground"/>.</summary>
    public string SurfaceElevated { get; set; } = "#4D1E2530";

    public string Border { get; set; } = "#3DFFFFFF";

    public double ShadowOpacity { get; set; } = 0.22;

    public double BlurAmount { get; set; } = 0;

    public double Spacing { get; set; } = 10;

    /// <summary>Shared fade / emphasis duration in milliseconds.</summary>
    public double MotionDurationMs { get; set; } = 180;

    public string FontFamily { get; set; } = "Segoe UI Variable Display";

    public double CornerRadius { get; set; } = 18;

    /// <summary>0..1 overall widget surface opacity hint (lower = airier glass).</summary>
    public double Transparency { get; set; } = 0.55;

    public double WidgetMinWidth { get; set; } = 200;

    public double WidgetMinHeight { get; set; } = 120;

    public static ThemeDefinition CreateDefault() => new();
}
