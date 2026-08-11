using SecretBase.Core.Desktop;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;

namespace SecretBase.Core.Tests;

public class ClockDisplayFormatterTests
{
    [Fact]
    public void FormatTime_Uses24HourWithSeconds()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 19, 42, 31, TimeSpan.FromHours(9)));
        var config = new ClockWidgetConfiguration { Use24HourFormat = true, ShowSeconds = true, ShowDate = true };

        var (time, date) = ClockDisplayFormatter.Format(provider, config);

        Assert.Equal("19:42:31", time);
        Assert.Equal("2026 / 08 / 11", date);
    }

    [Fact]
    public void FormatDate_CanBeHidden()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 7, 5, 9, TimeSpan.Zero));
        var config = new ClockWidgetConfiguration { ShowDate = false };

        var (_, date) = ClockDisplayFormatter.Format(provider, config);
        Assert.Equal(string.Empty, date);
    }
}

public class WidgetInstanceTests
{
    [Fact]
    public void DefaultClock_HasStableIdentityAndGeometry()
    {
        var clock = DefaultWidgetFactory.CreateDefaultClock();

        Assert.Equal(WidgetTypes.Clock, clock.Type);
        Assert.Equal(RoomId.DefaultRoomId, clock.RoomId);
        Assert.Equal(48, clock.Position.X);
        Assert.Equal(48, clock.Position.Y);
        Assert.Equal(280, clock.Size.Width);
        Assert.Equal(160, clock.Size.Height);
        Assert.Contains(nameof(ClockWidgetConfiguration.Use24HourFormat), clock.Configuration.Keys);
    }

    [Fact]
    public void WidgetSize_ClampsToMinimum()
    {
        var size = new WidgetSize(10, 10);
        size.Clamp(200, 120);
        Assert.Equal(200, size.Width);
        Assert.Equal(120, size.Height);
    }

    [Fact]
    public void ClockConfiguration_RoundTripsThroughDictionary()
    {
        var original = new ClockWidgetConfiguration
        {
            Use24HourFormat = true,
            ShowSeconds = false,
            ShowDate = true
        };

        var restored = ClockWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.True(restored.Use24HourFormat);
        Assert.False(restored.ShowSeconds);
        Assert.True(restored.ShowDate);
    }

    [Fact]
    public void DesktopLayout_CreateDefault_IncludesClock()
    {
        var layout = DesktopLayout.CreateDefault();
        Assert.Equal(1, layout.SchemaVersion);
        Assert.Single(layout.Widgets);
        Assert.Equal(WidgetTypes.Clock, layout.Widgets[0].Type);
    }
}

file sealed class FixedTimeProvider(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}
