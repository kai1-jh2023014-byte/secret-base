using SecretBase.Core.Desktop;
using SecretBase.Core.Music;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Tests;

public class MusicSourceTests
{
    [Fact]
    public void MusicSourceType_IncludesExpectedValues()
    {
        Assert.Equal(0, (int)MusicSourceType.Spotify);
        Assert.Equal(1, (int)MusicSourceType.YouTube);
        Assert.Equal(2, (int)MusicSourceType.Web);
        Assert.Equal(3, (int)MusicSourceType.Local);
    }
}

public class MusicProviderTests
{
    [Fact]
    public void OpenWebProvider_ResolvesSpotifyUrl()
    {
        var source = new MusicSource
        {
            Type = MusicSourceType.Spotify,
            Name = "Spotify",
            Url = "open.spotify.com/playlist/demo"
        };

        Assert.True(OpenWebMusicProvider.Instance.TryResolveOpenUrl(source, out var url, out var error));
        Assert.Null(error);
        Assert.StartsWith("https://open.spotify.com/", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenWebProvider_RejectsDangerousScheme()
    {
        var source = new MusicSource
        {
            Type = MusicSourceType.Web,
            Name = "Bad",
            Url = "javascript:alert(1)"
        };

        Assert.False(OpenWebMusicProvider.Instance.TryResolveOpenUrl(source, out _, out var error));
        Assert.Equal(WebUrlValidator.BlockedMessage, error);
    }

    [Fact]
    public void LocalProvider_DoesNotOpenUrl()
    {
        var source = new MusicSource { Type = MusicSourceType.Local, Name = "Local" };
        Assert.True(LocalMusicProvider.Instance.CanHandle(MusicSourceType.Local));
        Assert.False(LocalMusicProvider.Instance.TryResolveOpenUrl(source, out var url, out var error));
        Assert.Null(url);
        Assert.Contains("not available yet", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MusicService_RoutesByType()
    {
        var service = new MusicService();
        Assert.True(service.TryResolveOpenUrl(
            new MusicSource { Type = MusicSourceType.YouTube, Name = "YT", Url = "https://music.youtube.com/" },
            out var url,
            out _));
        Assert.Equal("https://music.youtube.com/", url);

        Assert.False(service.TryResolveOpenUrl(
            new MusicSource { Type = MusicSourceType.Local, Name = "Local" },
            out _,
            out _));
    }

    [Fact]
    public void Capabilities_OpenInWidget_DoesNotImplyApi()
    {
        var caps = MusicProviderCapabilities.OpenInWidget;
        Assert.True(caps.HasFlag(MusicProviderCapabilities.OpenInWidget));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.Authentication));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.Search));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.NowPlaying));
    }
}

public class MusicWidgetConfigurationTests
{
    [Fact]
    public void CreateDefault_HasSpotifyYouTubeLocal()
    {
        var config = MusicWidgetConfiguration.CreateDefault();
        Assert.Equal(3, config.Sources.Count);
        Assert.Contains(config.Sources, s => s.Type == MusicSourceType.Spotify);
        Assert.Contains(config.Sources, s => s.Type == MusicSourceType.YouTube);
        Assert.Contains(config.Sources, s => s.Type == MusicSourceType.Local);
    }

    [Fact]
    public void RoundTrip_PreservesSourcesAndActiveId()
    {
        var original = new MusicWidgetConfiguration
        {
            ActiveSourceId = "spotify-default",
            Sources =
            [
                new MusicSource
                {
                    Id = "spotify-default",
                    Type = MusicSourceType.Spotify,
                    Name = "Spotify",
                    Url = "https://open.spotify.com/",
                    IsEnabled = true
                },
                new MusicSource
                {
                    Id = "yt-1",
                    Type = MusicSourceType.YouTube,
                    Name = "YouTube Music",
                    Url = "https://music.youtube.com/",
                    IsEnabled = true
                }
            ]
        };

        var restored = MusicWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal("spotify-default", restored.ActiveSourceId);
        Assert.Equal(2, restored.Sources.Count);
        Assert.Equal("Spotify", restored.Sources[0].Name);
        Assert.Equal(MusicSourceType.Spotify, restored.Sources[0].Type);
        Assert.Equal("https://open.spotify.com/", restored.Sources[0].Url);
    }

    [Fact]
    public void FromDictionary_RejectsInvalidSourceUrl()
    {
        var bag = new Dictionary<string, System.Text.Json.JsonElement>
        {
            ["Sources"] = System.Text.Json.JsonSerializer.SerializeToElement(new[]
            {
                new Dictionary<string, object?>
                {
                    ["Id"] = "bad",
                    ["Type"] = "Web",
                    ["Name"] = "Evil",
                    ["Url"] = "file:///C:/Windows/notepad.exe",
                    ["IsEnabled"] = true
                },
                new Dictionary<string, object?>
                {
                    ["Id"] = "ok",
                    ["Type"] = "Spotify",
                    ["Name"] = "Spotify",
                    ["Url"] = "https://open.spotify.com/",
                    ["IsEnabled"] = true
                }
            })
        };

        var restored = MusicWidgetConfiguration.FromDictionary(bag);
        Assert.Single(restored.Sources);
        Assert.Equal("Spotify", restored.Sources[0].Name);
    }

    [Fact]
    public void ToDictionary_DropsInvalidWebSources()
    {
        var config = new MusicWidgetConfiguration
        {
            Sources =
            [
                new MusicSource
                {
                    Id = "1",
                    Type = MusicSourceType.Web,
                    Name = "Bad",
                    Url = "javascript:alert(1)"
                },
                new MusicSource
                {
                    Id = "2",
                    Type = MusicSourceType.Local,
                    Name = "Local"
                }
            ]
        };

        var restored = MusicWidgetConfiguration.FromDictionary(config.ToDictionary());
        Assert.Single(restored.Sources);
        Assert.Equal(MusicSourceType.Local, restored.Sources[0].Type);
        Assert.Null(restored.Sources[0].Url);
    }

    [Fact]
    public void CreateMusic_DoesNotSeedDefaultLayout()
    {
        var widget = DefaultWidgetFactory.CreateMusic();
        Assert.Equal(WidgetTypes.Music, widget.Type);
        var layout = DesktopLayout.CreateDefault();
        Assert.DoesNotContain(layout.Widgets, w => w.Type == WidgetTypes.Music);
    }
}
