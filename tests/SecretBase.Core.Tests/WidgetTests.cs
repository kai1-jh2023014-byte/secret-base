using System.Globalization;
using SecretBase.Core.Desktop;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Text;

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
        Assert.Contains("August 11", date, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatDate_CanBeHidden()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 8, 11, 7, 5, 9, TimeSpan.Zero));
        var config = new ClockWidgetConfiguration { ShowDate = false };

        var (_, date) = ClockDisplayFormatter.Format(provider, config);
        Assert.Equal(string.Empty, date);
    }

    [Fact]
    public void FormatDate_LargeStyle_UsesSingleLineTypography()
    {
        var provider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 22, 32, 0, TimeSpan.FromHours(9)));
        var config = new ClockWidgetConfiguration
        {
            DisplayStyle = ClockWidgetConfiguration.StyleLarge,
            ShowDate = true,
            Use24HourFormat = true,
            ShowSeconds = false
        };

        var (time, date) = ClockDisplayFormatter.Format(provider, config);
        Assert.Equal("22:32", time);
        Assert.Equal("Tuesday, September 01", date);
    }

    [Fact]
    public void FormatDate_AlwaysUsesEnglishRegardlessOfThreadCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ja-JP");
            var provider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 1, 22, 32, 0, TimeSpan.FromHours(9)));
            var config = new ClockWidgetConfiguration
            {
                DisplayStyle = ClockWidgetConfiguration.StyleLarge,
                ShowDate = true
            };

            var date = ClockDisplayFormatter.FormatDate(provider.GetLocalNow(), config);
            Assert.Equal("Tuesday, September 01", date);
            Assert.DoesNotContain("火曜日", date, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void NormalizeStyle_MapsLegacyDigitalToLarge()
    {
        Assert.Equal(ClockWidgetConfiguration.StyleLarge, ClockWidgetConfiguration.NormalizeStyle("digital"));
        Assert.Equal(ClockWidgetConfiguration.StyleBase, ClockWidgetConfiguration.NormalizeStyle("base"));
        Assert.True(ClockWidgetConfiguration.IsLargeTypography(ClockWidgetConfiguration.StyleLarge));
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
        Assert.Equal(200, clock.Size.Height);
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
            DisplayStyle = ClockWidgetConfiguration.StyleLarge,
            Use24HourFormat = true,
            ShowSeconds = false,
            ShowDate = true,
            SizeScale = 2.5,
            DateScale = 1.6
        };

        var restored = ClockWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal(ClockWidgetConfiguration.StyleLarge, restored.DisplayStyle);
        Assert.True(restored.Use24HourFormat);
        Assert.False(restored.ShowSeconds);
        Assert.True(restored.ShowDate);
        Assert.Equal(2.5, restored.SizeScale);
        Assert.Equal(1.6, restored.DateScale);
    }

    [Fact]
    public void ClockConfiguration_FromDictionary_MigratesDigitalToLarge()
    {
        var legacy = new ClockWidgetConfiguration { DisplayStyle = "digital" }.ToDictionary();
        // Force raw "digital" into the bag to simulate older layouts.
        legacy[nameof(ClockWidgetConfiguration.DisplayStyle)] =
            System.Text.Json.JsonSerializer.SerializeToElement("digital");
        var restored = ClockWidgetConfiguration.FromDictionary(legacy);
        Assert.Equal(ClockWidgetConfiguration.StyleLarge, restored.DisplayStyle);
    }

    [Fact]
    public void DefaultText_HasStableIdentityAndGeometry()
    {
        var text = DefaultWidgetFactory.CreateDefaultText();

        Assert.Equal(WidgetTypes.Text, text.Type);
        Assert.Equal(RoomId.DefaultRoomId, text.RoomId);
        Assert.Equal(48, text.Position.X);
        Assert.Equal(240, text.Position.Y);
        Assert.Equal(320, text.Size.Width);
        Assert.Equal(180, text.Size.Height);
        Assert.Contains(nameof(TextWidgetConfiguration.Text), text.Configuration.Keys);
        Assert.Contains(nameof(TextWidgetConfiguration.FontSize), text.Configuration.Keys);
        Assert.Contains(nameof(TextWidgetConfiguration.TextAlignment), text.Configuration.Keys);
    }

    [Fact]
    public void TextConfiguration_RoundTripsThroughDictionary()
    {
        var original = new TextWidgetConfiguration
        {
            Text = "Ship the Text Widget",
            FontSize = 22,
            TextAlignment = TextWidgetAlignment.Center
        };

        var restored = TextWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal("Ship the Text Widget", restored.Text);
        Assert.Equal(22, restored.FontSize);
        Assert.Equal(TextWidgetAlignment.Center, restored.TextAlignment);
    }

    [Fact]
    public void TextConfiguration_FromDictionary_UsesDefaultsForMissingKeys()
    {
        var restored = TextWidgetConfiguration.FromDictionary(new Dictionary<string, System.Text.Json.JsonElement>());
        Assert.Equal(TextWidgetConfiguration.DefaultPlaceholderText, restored.Text);
        Assert.Equal(18, restored.FontSize);
        Assert.Equal(TextWidgetAlignment.Left, restored.TextAlignment);
    }

    [Fact]
    public void DesktopLayout_CreateDefault_IncludesClockAndText()
    {
        var layout = DesktopLayout.CreateDefault();
        Assert.Equal(DesktopLayout.CurrentSchemaVersion, layout.SchemaVersion);
        Assert.Equal(2, layout.Widgets.Count);
        Assert.Equal(WidgetTypes.Clock, layout.Widgets[0].Type);
        Assert.Equal(WidgetTypes.Text, layout.Widgets[1].Type);
        Assert.Empty(layout.Blocks);
    }
}

file sealed class FixedTimeProvider(DateTimeOffset instant) : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => instant;
}
