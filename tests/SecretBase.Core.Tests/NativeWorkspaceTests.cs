using SecretBase.Core.Music;
using SecretBase.Core.Widgets.Clock;

namespace SecretBase.Core.Tests;

public class SpotifyMusicProviderMappingTests
{
    [Fact]
    public async Task MusicCommand_GetPlaybackState_RequiresProvider()
    {
        var service = new MusicService([new DemoCatalogMusicProvider()]);
        var commands = new MusicCommandService(service);
        var result = await commands.ExecuteAsync(MusicCommand.GetPlaybackState("spotify"));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MusicCommand_ConnectProvider_RequiresKnownProvider()
    {
        var service = new MusicService([new DemoCatalogMusicProvider()]);
        var commands = new MusicCommandService(service);
        var result = await commands.ExecuteAsync(MusicCommand.ConnectProvider("missing"));
        Assert.False(result.Succeeded);
    }
}

public class ClockCustomizationTests
{
    [Fact]
    public void ClockConfiguration_RoundTripsStyleAndScale()
    {
        var config = new ClockWidgetConfiguration
        {
            DisplayStyle = ClockWidgetConfiguration.StyleFocus,
            Use24HourFormat = false,
            ShowSeconds = false,
            ShowDate = true,
            SizeScale = 2.4,
            DateScale = 1.8
        };
        var restored = ClockWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.Equal(ClockWidgetConfiguration.StyleFocus, restored.DisplayStyle);
        Assert.False(restored.Use24HourFormat);
        Assert.False(restored.ShowSeconds);
        Assert.Equal(2.4, restored.SizeScale);
        Assert.Equal(1.8, restored.DateScale);
    }

    [Fact]
    public void ClockConfiguration_ClampsSizeAndDateScale()
    {
        Assert.Equal(
            ClockWidgetConfiguration.SizeScaleMax,
            ClockWidgetConfiguration.ClampSizeScale(9));
        Assert.Equal(
            ClockWidgetConfiguration.DateScaleMin,
            ClockWidgetConfiguration.ClampDateScale(0.1));
    }

    [Fact]
    public void ClockFormatter_FocusStyle_UsesReadableDate()
    {
        var config = new ClockWidgetConfiguration { DisplayStyle = ClockWidgetConfiguration.StyleFocus, ShowDate = true };
        var date = ClockDisplayFormatter.FormatDate(new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), config);
        Assert.Contains("September", date, StringComparison.Ordinal);
    }
}
