using SecretBase.Core.Themes;

namespace SecretBase.Core.Tests;

public class AppearanceSystemTests
{
    [Fact]
    public void DefaultTheme_HasStatusAndAppearanceTokens()
    {
        var theme = ThemeDefinition.CreateDefault();
        Assert.Equal(ThemeMigrator.CurrentSchema, theme.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(theme.StatusSuccess));
        Assert.False(string.IsNullOrWhiteSpace(theme.StatusWarning));
        Assert.False(string.IsNullOrWhiteSpace(theme.StatusError));
        Assert.Equal(AppearanceDensity.Comfortable, theme.Density);
        Assert.Equal(AccentPalette.Jade, theme.AccentName);
        Assert.False(theme.ReducedMotion);
        Assert.True(theme.TitleSize >= 36);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("Midnight")]
    [InlineData("Soft")]
    [InlineData("Minimal")]
    [InlineData("Glass")]
    [InlineData("High Contrast")]
    public void GalleryThemes_AreCompleteVisualSystems(string name)
    {
        var theme = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(theme, name);
        Assert.Equal(name, theme.DisplayName);
        Assert.InRange(theme.Transparency, 0.35, 1.0);
        Assert.False(string.IsNullOrWhiteSpace(theme.SurfaceElevated));
        Assert.NotEqual(theme.StatusError, theme.Accent);
        Assert.True(theme.TitleSize >= 32);
        Assert.True(theme.BodySize >= 11);
    }

    [Fact]
    public void HighContrast_ReducesMotionAndRaisesContrast()
    {
        var theme = ThemeDefinition.CreateDefault();
        ThemePresets.ApplyPreset(theme, VisualThemeNames.HighContrast);
        Assert.True(theme.ReducedMotion);
        Assert.Equal(0, theme.MotionDurationMs);
        Assert.Equal(1.0, theme.Transparency);
        Assert.True(ThemeColor.ContrastRatio(theme.WidgetForeground, theme.WidgetBackground) >= 7);
    }

    [Fact]
    public void Accent_DoesNotRetintStatusColors()
    {
        var theme = ThemeDefinition.CreateDefault();
        var warning = theme.StatusWarning;
        var error = theme.StatusError;
        AppearanceComposer.ApplyAccent(theme, AccentPalette.Purple);
        Assert.Equal("#FF9B7ED9", theme.Accent);
        Assert.Equal(AccentPalette.Purple, theme.AccentName);
        Assert.Equal(warning, theme.StatusWarning);
        Assert.Equal(error, theme.StatusError);
        Assert.True(ThemeColor.ContrastRatio(theme.OnAccent, theme.Accent) >= 3);
    }

    [Fact]
    public void DensityAndShape_ChangeSpacingAndRadiusTogether()
    {
        var theme = ThemeDefinition.CreateDefault();
        AppearanceComposer.ApplyDensity(theme, AppearanceDensity.Compact);
        Assert.Equal(6, theme.Spacing);
        Assert.True(theme.TitleSize < 44);
        AppearanceComposer.ApplyDensity(theme, AppearanceDensity.Spacious);
        Assert.Equal(14, theme.Spacing);
        AppearanceComposer.ApplyShape(theme, AppearanceShape.Sharp);
        Assert.Equal(6, theme.CornerRadius);
        AppearanceComposer.ApplyShape(theme, AppearanceShape.Soft);
        Assert.Equal(22, theme.CornerRadius);
        AppearanceComposer.ApplyShape(theme, AppearanceShape.Balanced);
        Assert.Equal(18, theme.CornerRadius);
    }

    [Fact]
    public void ReducedMotion_ZeroesDuration()
    {
        var theme = ThemeDefinition.CreateDefault();
        AppearanceComposer.ApplyMotion(theme, AppearanceMotion.Reduced);
        Assert.True(theme.ReducedMotion);
        Assert.Equal(0, theme.MotionDurationMs);
        AppearanceComposer.ApplyMotion(theme, AppearanceMotion.Subtle);
        Assert.False(theme.ReducedMotion);
        Assert.True(theme.MotionDurationMs >= 120);
    }

    [Fact]
    public void Composer_KeepsCustomAccent()
    {
        var theme = ThemeDefinition.CreateDefault();
        AppearanceComposer.Apply(theme, new AppearanceProfile
        {
            VisualTheme = VisualThemeNames.Dark,
            AccentName = AccentPalette.Custom,
            CustomAccent = "#FF4A90D9",
            Density = AppearanceDensity.Compact,
            Shape = AppearanceShape.Sharp,
            Motion = AppearanceMotion.Standard
        });
        Assert.Equal("Dark", theme.DisplayName);
        Assert.Equal("#FF4A90D9", theme.Accent);
        Assert.Equal(AppearanceDensity.Compact, theme.Density);
        Assert.Equal(6, theme.CornerRadius);
        Assert.Equal(220, theme.MotionDurationMs);
    }

    [Fact]
    public void Migrator_FillsLegacyTheme()
    {
        var legacy = new ThemeDefinition
        {
            SchemaVersion = 1,
            DisplayName = "Midnight",
            Accent = "#FF6C8CFF",
            WidgetBackground = "#CC141A2C",
            SurfaceElevated = "",
            Density = "",
            StatusSuccess = ""
        };
        ThemeMigrator.Normalize(legacy);
        Assert.Equal(ThemeMigrator.CurrentSchema, legacy.SchemaVersion);
        Assert.False(string.IsNullOrWhiteSpace(legacy.StatusSuccess));
        Assert.Equal(legacy.WidgetBackground, legacy.SurfaceElevated);
        Assert.Equal(AccentPalette.Blue, legacy.AccentName);
    }

    [Fact]
    public void Profile_FormatsExplicitPreferencesOnly()
    {
        var profile = new AppearanceProfile
        {
            VisualTheme = "Midnight",
            AccentName = "Purple",
            Density = "Compact",
            Shape = "Sharp",
            Motion = "Subtle"
        };
        var line = profile.Format();
        Assert.Contains("Midnight", line, StringComparison.Ordinal);
        Assert.Contains("Purple", line, StringComparison.Ordinal);
        Assert.DoesNotContain("personality", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UxCopy_NeverSaysNoData()
    {
        Assert.DoesNotContain("No data", UxCopy.MemoryEmpty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No data", UxCopy.WorkspaceEmpty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remember", UxCopy.MemoryEmpty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project", UxCopy.WorkspaceEmpty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandPalette_GroupsActions()
    {
        var items = SecretBase.Core.Commands.CommandPalette.Build(
            "",
            new SecretBase.Core.State.UserState { Now = DateTimeOffset.UtcNow },
            new SecretBase.Core.Situation.CurrentSituation(),
            new SecretBase.Core.Intent.DetectedIntent { Kind = SecretBase.Core.Intent.DetectedIntentKind.Unknown },
            [],
            [],
            [],
            new SecretBase.Core.Todo.TodoList());
        Assert.Contains(items, item => item.Action == "continue" && item.Group == "Actions");
        Assert.Contains(items, item => item.Action == "briefing" && item.Group == "Calendar");
        Assert.Contains("·", SecretBase.Core.Commands.CommandPalette.FormatLine(items[0]));
    }

    [Fact]
    public void Presence_ShowsContextThenSuggestion()
    {
        var text = SecretBase.Core.Assistant.BaseAiPresence.Format(
            new SecretBase.Core.Assistant.AssistantProviderStatusInfo
            {
                ProviderId = SecretBase.Core.Assistant.AssistantProviderIds.Local,
                IsConfigured = true
            },
            new SecretBase.Core.State.UserState
            {
                Greeting = "Good evening",
                CurrentProjectName = "Secret Base"
            },
            new SecretBase.Core.Situation.CurrentSituation { ProjectName = "Secret Base" });
        Assert.Contains("Local", text, StringComparison.Ordinal);
        Assert.Contains("Good evening", text, StringComparison.Ordinal);
        Assert.Contains("Secret Base", text, StringComparison.Ordinal);
        Assert.Contains("Continue", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ChatGPT", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FriendlyAiError_ExplainsWhatStillWorks()
    {
        Assert.Contains("workspace", UxCopy.FriendlyAiError("AI provider is unavailable."), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Could not load Calendar.", UxCopy.FriendlyAiError("Could not load Calendar."));
    }

    [Fact]
    public void LayoutMetrics_ChangeWithWidthAndDensity()
    {
        var large = SecretBase.Core.Desktop.DesktopWidgetLayout.MetricsFor(1600, AppearanceDensity.Spacious);
        var overlay = SecretBase.Core.Desktop.DesktopWidgetLayout.MetricsFor(500, AppearanceDensity.Compact);
        Assert.Equal(SecretBase.Core.Desktop.DesktopWidgetLayout.BandLarge, large.Band);
        Assert.Equal(SecretBase.Core.Desktop.DesktopWidgetLayout.BandOverlay, overlay.Band);
        Assert.True(large.Margin > overlay.Margin);
    }
}
