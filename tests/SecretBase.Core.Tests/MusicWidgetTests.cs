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
    public void OpenWebProvider_ResolvesSpotifyUrl_ButNoSearch()
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
        Assert.False(OpenWebMusicProvider.Instance.Capabilities.HasFlag(MusicProviderCapabilities.Search));
        Assert.False(OpenWebMusicProvider.Instance.Capabilities.HasFlag(MusicProviderCapabilities.Playback));
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
    public async Task DemoCatalog_SearchAndPlay()
    {
        var demo = new DemoCatalogMusicProvider();
        Assert.True(demo.Capabilities.HasFlag(MusicProviderCapabilities.Search));
        Assert.True(demo.Capabilities.HasFlag(MusicProviderCapabilities.Playback));

        var found = await demo.SearchAsync("Lilac");
        Assert.Contains(found, t => t.Title.Contains("Lilac", StringComparison.OrdinalIgnoreCase));

        await demo.PlayAsync(found[0]);
        Assert.True(demo.IsPlaying);
        Assert.Equal(found[0].Id, demo.CurrentTrack!.Id);

        await demo.PauseAsync();
        Assert.False(demo.IsPlaying);
        await demo.ResumeAsync();
        Assert.True(demo.IsPlaying);
    }

    [Fact]
    public void Capabilities_OpenInWidget_DoesNotImplyApi()
    {
        var caps = MusicProviderCapabilities.OpenInWidget;
        Assert.True(caps.HasFlag(MusicProviderCapabilities.OpenInWidget));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.Authentication));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.Search));
        Assert.False(caps.HasFlag(MusicProviderCapabilities.Playback));
    }
}

public class MusicCommandServiceTests
{
    [Fact]
    public async Task SearchTrack_ReturnsDemoHits()
    {
        var service = new MusicCommandService(new MusicService());
        var result = await service.ExecuteAsync(MusicCommand.SearchTrack("Mrs. GREEN"));
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.Tracks);
        Assert.All(result.Tracks, t => Assert.Equal(DemoCatalogMusicProvider.Id, t.ProviderId));
    }

    [Fact]
    public async Task SearchTrack_RejectsEmptyAndDangerousQuery()
    {
        var service = new MusicCommandService(new MusicService());
        Assert.False((await service.ExecuteAsync(MusicCommand.SearchTrack("  "))).Succeeded);
        Assert.False((await service.ExecuteAsync(MusicCommand.SearchTrack("https://evil.example"))).Succeeded);
        Assert.False((await service.ExecuteAsync(MusicCommand.SearchTrack("../etc/passwd"))).Succeeded);
    }

    [Fact]
    public async Task PlayTrack_ThenPauseResumeNext()
    {
        var music = new MusicService([new DemoCatalogMusicProvider()]);
        var service = new MusicCommandService(music);
        var search = await service.ExecuteAsync(MusicCommand.SearchTrack("Pretender"));
        Assert.True(search.Succeeded);
        var track = search.Tracks[0];

        var play = await service.ExecuteAsync(MusicCommand.PlayTrack(track));
        Assert.True(play.Succeeded);
        Assert.True(play.IsPlaying);
        Assert.Equal("Pretender", play.CurrentTrack!.Title);

        var pause = await service.ExecuteAsync(MusicCommand.Pause());
        Assert.True(pause.Succeeded);
        Assert.False(pause.IsPlaying);

        var resume = await service.ExecuteAsync(MusicCommand.Resume());
        Assert.True(resume.Succeeded);
        Assert.True(resume.IsPlaying);

        var next = await service.ExecuteAsync(MusicCommand.Next());
        Assert.True(next.Succeeded);
        Assert.NotNull(next.CurrentTrack);
    }

    [Fact]
    public async Task PlayTrack_UnsupportedProvider_Fails()
    {
        var music = new MusicService([OpenWebMusicProvider.Instance]);
        var service = new MusicCommandService(music);
        var result = await service.ExecuteAsync(MusicCommand.PlayTrack(new MusicTrack
        {
            Id = "x",
            Title = "Nope",
            ProviderId = "web-open"
        }));
        Assert.False(result.Succeeded);
        Assert.Contains("supports playback", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pause_WithoutTrack_Fails()
    {
        var service = new MusicCommandService(new MusicService([new DemoCatalogMusicProvider()]));
        var result = await service.ExecuteAsync(MusicCommand.Pause());
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Search_SkipsDisconnectedAuthProvider_AndKeepsDemoCatalog()
    {
        var music = new MusicService(
        [
            new DisconnectedAuthSearchProvider(),
            new DemoCatalogMusicProvider()
        ]);
        var result = await new MusicCommandService(music).ExecuteAsync(MusicCommand.SearchTrack("Lilac"));
        Assert.True(result.Succeeded);
        Assert.Contains(result.Tracks, t => t.ProviderId == DemoCatalogMusicProvider.Id);
        Assert.DoesNotContain(result.Tracks, t => t.ProviderId == "spotify");
    }

    [Fact]
    public async Task Search_ApiRefusal_OffersSpotifyWebSearch()
    {
        var music = new MusicService([new RefusingSpotifySearchProvider()]);
        var result = await new MusicCommandService(music).ExecuteAsync(MusicCommand.SearchTrack("Pretender"));
        Assert.False(result.Succeeded);
        Assert.True(SpotifyWebSearch.TryCreateSearchUrl("Pretender", out var expected, out _));
        Assert.Equal(expected, result.WebSearchUrl);
        Assert.StartsWith("https://open.spotify.com/search/", result.WebSearchUrl, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpotifyWebSearch_RejectsSchemeLikeQuery()
    {
        Assert.False(SpotifyWebSearch.TryCreateSearchUrl("https://evil.example", out var url, out _));
        Assert.Null(url);
    }

    [Fact]
    public async Task Connect_RemembersClientId_BeforeConnect()
    {
        var provider = new RememberingMusicProvider();
        var service = new MusicCommandService(new MusicService([provider]));
        var result = await service.ExecuteAsync(MusicCommand.ConnectProvider("spotify", "0123456789abcdef0123456789abcdef", "secret"));
        Assert.True(result.Succeeded);
        Assert.Equal("0123456789abcdef0123456789abcdef", provider.SavedClientId);
        Assert.Equal(MusicAuthStatus.Connected, result.AuthStatus);
        Assert.Equal(SpotifyOAuth.RedirectUri, provider.RedirectUri);
    }

    private sealed class DisconnectedAuthSearchProvider : IMusicProvider
    {
        public string ProviderId => "spotify";

        public string DisplayName => "Spotify";

        public MusicProviderCapabilities Capabilities =>
            MusicProviderCapabilities.Search | MusicProviderCapabilities.Authentication;

        public MusicAuthStatus AuthStatus => MusicAuthStatus.Disconnected;

        public MusicTrack? CurrentTrack => null;

        public bool IsPlaying => false;

        public bool CanHandle(MusicSourceType type) => false;

        public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
        {
            url = null;
            error = null;
            return false;
        }

        public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify is not connected.");

        public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task PauseAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ResumeAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task NextAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task PreviousAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RefusingSpotifySearchProvider : IMusicProvider
    {
        public string ProviderId => "spotify";

        public string DisplayName => "Spotify";

        public MusicProviderCapabilities Capabilities =>
            MusicProviderCapabilities.Search | MusicProviderCapabilities.Authentication;

        public MusicAuthStatus AuthStatus => MusicAuthStatus.Connected;

        public MusicTrack? CurrentTrack => null;

        public bool IsPlaying => false;

        public bool CanHandle(MusicSourceType type) => false;

        public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
        {
            url = null;
            error = null;
            return false;
        }

        public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Spotify's catalog API refused this account.");

        public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PreviousAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RememberingMusicProvider : IMusicProvider, IMusicOAuthClientSource
    {
        public string? SavedClientId { get; private set; }

        public string ProviderId => "spotify";

        public string DisplayName => "Spotify";

        public MusicProviderCapabilities Capabilities => MusicProviderCapabilities.Authentication;

        public MusicAuthStatus AuthStatus { get; private set; } = MusicAuthStatus.NotConfigured;

        public string RedirectUri => SpotifyOAuth.RedirectUri;

        public MusicTrack? CurrentTrack => null;

        public bool IsPlaying => false;

        public bool CanHandle(MusicSourceType type) => false;

        public bool TryResolveOpenUrl(MusicSource source, out string? url, out string? error)
        {
            url = null;
            error = null;
            return false;
        }

        public void RememberClient(string clientId, string? clientSecret) => SavedClientId = clientId;

        public Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MusicTrack>>(Array.Empty<MusicTrack>());

        public Task PlayAsync(MusicTrack track, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task NextAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PreviousAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            AuthStatus = MusicAuthStatus.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshPlaybackStateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

public class MusicTrackSerializationTests
{
    [Fact]
    public void Configuration_RoundTripsCurrentTrackWithoutArtwork()
    {
        var original = new MusicWidgetConfiguration
        {
            ActiveSourceId = "spotify-default",
            CurrentTrack = new MusicTrack
            {
                Id = "demo-lilac",
                Title = "Lilac",
                Artist = "Mrs. GREEN APPLE",
                Album = "Antenna",
                ProviderId = DemoCatalogMusicProvider.Id,
                Source = "Demo catalog",
                ArtworkUrl = "https://example.com/art.jpg"
            },
            Sources =
            [
                new MusicSource
                {
                    Id = "spotify-default",
                    Type = MusicSourceType.Spotify,
                    Name = "Spotify",
                    Url = "https://open.spotify.com/",
                    IsEnabled = true
                }
            ]
        };

        var restored = MusicWidgetConfiguration.FromDictionary(original.ToDictionary());
        Assert.Equal("Lilac", restored.CurrentTrack!.Title);
        Assert.Equal("Mrs. GREEN APPLE", restored.CurrentTrack.Artist);
        Assert.Equal(DemoCatalogMusicProvider.Id, restored.CurrentTrack.ProviderId);
        Assert.Null(restored.CurrentTrack.ArtworkUrl);
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
