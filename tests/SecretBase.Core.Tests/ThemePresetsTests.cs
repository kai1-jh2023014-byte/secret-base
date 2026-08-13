using SecretBase.Core.Themes;

namespace SecretBase.Core.Tests;

public class ThemePresetsTests
{
    [Fact]
    public void ApplyPreset_Midnight_ChangesWidgetColors()
    {
        var theme = ThemeDefinition.CreateDefault();
        var originalBg = theme.WidgetBackground;

        ThemePresets.ApplyPreset(theme, "Midnight");

        Assert.Equal("Midnight", theme.DisplayName);
        Assert.NotEqual(originalBg, theme.WidgetBackground);
        Assert.Equal("#CC141A2C", theme.WidgetBackground);
        Assert.Equal("#FFF2F5FF", theme.WidgetForeground);
        Assert.Equal(14, theme.CornerRadius);
    }

    [Fact]
    public void ApplyPreset_Unknown_FallsBackToDefaultTokens()
    {
        var theme = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(theme, "Midnight");
        ThemePresets.ApplyPreset(theme, "not-a-real-preset");

        Assert.Equal("not-a-real-preset", theme.DisplayName);
        Assert.Equal("#CC243447", theme.WidgetBackground);
        Assert.Equal("#FFF4F0E6", theme.WidgetForeground);
        Assert.Equal(16, theme.CornerRadius);
    }

    [Fact]
    public void CopyVisualsTo_PreservesTargetId()
    {
        var source = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(source, "Ocean");
        var target = ThemeDefinition.CreateDefault();
        target.Id = "default";

        ThemePresets.CopyVisualsTo(source, target);

        Assert.Equal("default", target.Id);
        Assert.Equal(source.WidgetBackground, target.WidgetBackground);
        Assert.Equal(source.Accent, target.Accent);
        Assert.Equal(source.FontFamily, target.FontFamily);
        Assert.Equal("Ocean", target.DisplayName);
    }

    [Theory]
    [InlineData("Default")]
    [InlineData("Warm Paper")]
    [InlineData("Forest")]
    [InlineData("Soft Rose")]
    public void ApplyPreset_AllNamedPresets_SetOpaqueEnoughTransparency(string name)
    {
        var theme = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(theme, name);
        Assert.InRange(theme.Transparency, 0.35, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(theme.WidgetBackground));
        Assert.False(string.IsNullOrWhiteSpace(theme.WidgetForeground));
    }
}
