using SecretBase.Core.Themes;

namespace SecretBase.Core.Tests;

public class WidgetVisualSystemTests
{
    [Fact]
    public void DefaultTheme_UsesAtelierTokens()
    {
        var theme = ThemeDefinition.CreateDefault();
        Assert.Equal("Atelier", theme.DisplayName);
        Assert.Equal("#D9181E28", theme.WidgetBackground);
        Assert.Equal("#FFF3EFE6", theme.WidgetForeground);
        Assert.Equal("#FF7A9E86", theme.Accent);
        Assert.Equal(18, theme.CornerRadius);
        Assert.True(theme.Spacing >= 8);
        Assert.False(string.IsNullOrWhiteSpace(theme.Border));
        Assert.False(string.IsNullOrWhiteSpace(theme.SurfaceSecondary));
    }

    [Fact]
    public void CopyVisualsTo_CopiesElevationTokens()
    {
        var source = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(source, "Glass");
        var target = ThemeDefinition.CreateDefault();
        ThemePresets.CopyVisualsTo(source, target);
        Assert.Equal(source.BlurAmount, target.BlurAmount);
        Assert.Equal(source.Border, target.Border);
        Assert.Equal(source.SurfaceSecondary, target.SurfaceSecondary);
        Assert.Equal(source.Spacing, target.Spacing);
    }
}
