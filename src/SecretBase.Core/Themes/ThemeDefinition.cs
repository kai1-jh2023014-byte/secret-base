namespace SecretBase.Core.Themes;

/// <summary>
/// Theme tokens consumed by Desktop/Widgets. Values are stored as strings so JSON stays simple.
/// Colors use #AARRGGBB or #RRGGBB.
/// </summary>
public sealed class ThemeDefinition
{
    public int SchemaVersion { get; set; } = 1;

    public string Id { get; set; } = "default";

    public string DisplayName { get; set; } = "Default";

    public string Background { get; set; } = "#FF1A2332";

    public string BackgroundSecondary { get; set; } = "#FF2F4A3C";

    public string Foreground { get; set; } = "#FFF4F0E6";

    public string ForegroundMuted { get; set; } = "#FF9AA7B2";

    public string Accent { get; set; } = "#FF6B9F7A";

    public string WidgetBackground { get; set; } = "#CC243447";

    public string WidgetForeground { get; set; } = "#FFF4F0E6";

    public string SurfaceSecondary { get; set; } = "#33243447";

    public string Border { get; set; } = "#44FFFFFF";

    public double ShadowOpacity { get; set; } = 0.18;

    public double BlurAmount { get; set; } = 0;

    public double Spacing { get; set; } = 8;

    public string FontFamily { get; set; } = "Segoe UI Variable Display";

    public double CornerRadius { get; set; } = 16;

    /// <summary>0..1 overall widget surface opacity hint.</summary>
    public double Transparency { get; set; } = 0.85;

    public double WidgetMinWidth { get; set; } = 200;

    public double WidgetMinHeight { get; set; } = 120;

    public static ThemeDefinition CreateDefault() => new();
}
