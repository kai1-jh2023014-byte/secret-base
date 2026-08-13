using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Tests;

public class WebUrlValidatorTests
{
    [Theory]
    [InlineData("https://www.youtube.com/")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://example.com")]
    [InlineData("www.youtube.com")]
    [InlineData("youtube.com/feed")]
    public void TryNormalize_AllowsHttpHttpsAndBareHosts(string input)
    {
        Assert.True(WebUrlValidator.TryNormalize(input, out var normalized, out var error));
        Assert.NotNull(normalized);
        Assert.Null(error);
        Assert.StartsWith("http", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hi")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("about:blank")]
    [InlineData("ms-appx-web:///index.html")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalize_RejectsDangerousSchemes(string input)
    {
        Assert.False(WebUrlValidator.TryNormalize(input, out var normalized, out var error));
        Assert.Null(normalized);
        Assert.Equal(WebUrlValidator.BlockedMessage, error);
    }

    [Fact]
    public void TryNormalize_StripsUserInfo()
    {
        Assert.True(WebUrlValidator.TryNormalize("https://user:pass@example.com/path", out var normalized, out _));
        Assert.Equal("https://example.com/path", normalized);
    }
}

public class WebWidgetConfigurationTests
{
    [Fact]
    public void CreateDefault_UsesYouTubeExampleUrl()
    {
        var config = WebWidgetConfiguration.CreateDefault();
        Assert.Equal(WebWidgetConfiguration.DefaultUrl, config.Url);
        Assert.True(WebUrlValidator.IsAllowed(config.Url));
    }

    [Fact]
    public void RoundTrip_PreservesUrl()
    {
        var original = new WebWidgetConfiguration { Url = "https://github.com/" };
        var restored = WebWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal("https://github.com/", restored.Url);
    }

    [Fact]
    public void FromDictionary_MissingKey_UsesDefault()
    {
        var restored = WebWidgetConfiguration.FromDictionary(new Dictionary<string, System.Text.Json.JsonElement>());
        Assert.Equal(WebWidgetConfiguration.DefaultUrl, restored.Url);
    }

    [Fact]
    public void FromDictionary_RejectsDangerousUrl_FallsBackToDefault()
    {
        var bag = new Dictionary<string, System.Text.Json.JsonElement>
        {
            [nameof(WebWidgetConfiguration.Url)] =
                System.Text.Json.JsonSerializer.SerializeToElement("file:///etc/passwd")
        };

        var restored = WebWidgetConfiguration.FromDictionary(bag);
        Assert.Equal(WebWidgetConfiguration.DefaultUrl, restored.Url);
    }

    [Fact]
    public void ToDictionary_NormalizesBareHost()
    {
        var config = new WebWidgetConfiguration { Url = "www.youtube.com" };
        var restored = WebWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.StartsWith("https://", restored.Url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("youtube.com", restored.Url, StringComparison.OrdinalIgnoreCase);
    }
}

public class WebWidgetFactoryTests
{
    [Fact]
    public void CreateWeb_SetsTypeAndSafeConfiguration()
    {
        var widget = DefaultWidgetFactory.CreateWeb("https://www.youtube.com/");
        Assert.Equal(WidgetTypes.Web, widget.Type);
        Assert.NotEqual(Guid.Empty, widget.Id);
        Assert.Equal(560, widget.Size.Width);
        Assert.Equal(360, widget.Size.Height);

        var config = WebWidgetConfiguration.FromDictionary(widget.Configuration);
        Assert.Equal("https://www.youtube.com/", config.Url);
    }

    [Fact]
    public void CreateWeb_DoesNotReuseStableClockOrTextIds()
    {
        var clock = DefaultWidgetFactory.CreateDefaultClock();
        var text = DefaultWidgetFactory.CreateDefaultText();
        var web = DefaultWidgetFactory.CreateWeb();
        Assert.NotEqual(clock.Id, web.Id);
        Assert.NotEqual(text.Id, web.Id);
    }
}
