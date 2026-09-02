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
            SizeScale = 1.2
        };
        var restored = ClockWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.Equal(ClockWidgetConfiguration.StyleFocus, restored.DisplayStyle);
        Assert.False(restored.Use24HourFormat);
        Assert.False(restored.ShowSeconds);
        Assert.Equal(1.2, restored.SizeScale);
    }

    [Fact]
    public void ClockFormatter_FocusStyle_UsesReadableDate()
    {
        var config = new ClockWidgetConfiguration { DisplayStyle = ClockWidgetConfiguration.StyleFocus, ShowDate = true };
        var date = ClockDisplayFormatter.FormatDate(new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero), config);
        Assert.Contains("September", date, StringComparison.Ordinal);
    }
}
