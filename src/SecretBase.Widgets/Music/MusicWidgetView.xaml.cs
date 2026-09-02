using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Music;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Music;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Music;

/// <summary>
/// Native Music UI. Search / current track / transport go through
/// <see cref="MusicCommandService"/> → <see cref="IMusicProvider"/>.
/// No Host Bridge. WebView is not the primary surface.
/// </summary>
public sealed partial class MusicWidgetView : UserControl
{
    private MusicWidgetConfiguration _configuration = MusicWidgetConfiguration.CreateDefault();
    private MusicService _musicService = new();
    private MusicCommandService _commands = null!;
    private Action<MusicWidgetConfiguration>? _onConfigurationChanged;
    private Func<string, bool>? _openUrl;
    private ThemeDefinition? _theme;
    private readonly List<MusicTrack> _lastResults = [];
    private int _searchGate;
    private int _commandGate;

    private bool _connectDismissed;

    public MusicWidgetView()
    {
        InitializeComponent();
        _commands = new MusicCommandService(_musicService);
    }

    public void Initialize(
        MusicWidgetConfiguration configuration,
        Action<MusicWidgetConfiguration>? onConfigurationChanged = null,
        Func<string, bool>? openUrl = null,
        MusicService? musicService = null)
    {
        _configuration = configuration;
        _onConfigurationChanged = onConfigurationChanged;
        _openUrl = openUrl;
        if (musicService is not null)
        {
            _musicService = musicService;
            _commands = new MusicCommandService(_musicService);
        }

        if (_configuration.CurrentTrack is not null)
        {
            _commands.RememberTracks([_configuration.CurrentTrack]);
        }

        UpdateCurrentTrackUi(_configuration.CurrentTrack, isPlaying: false);
        UpdateTransportEnabled();
        RefreshConnectPanel();
        ResultsList.Children.Clear();
        ResultsList.Children.Add(CreateMuted("Search tracks from your connected provider."));
        StatusLabel.Text = string.Empty;
        SourceLabel.Text = DescribeSource();
        _ = RefreshPlaybackStateAsync();
    }

    private string DescribeSource()
    {
        var spotify = _musicService.Providers.FirstOrDefault(p => p.ProviderId == "spotify");
        if (spotify?.AuthStatus == MusicAuthStatus.Connected)
        {
            return "Source: Spotify";
        }

        return "Source: Demo catalog (connect Spotify for full search and playback control)";
    }

    private void RefreshConnectPanel()
    {
        var spotify = _musicService.Providers.FirstOrDefault(p => p.ProviderId == "spotify");
        var needsConnect = !_connectDismissed
                           && spotify is not null
                           && spotify.AuthStatus is MusicAuthStatus.NotConfigured or MusicAuthStatus.Disconnected;
        ConnectPanel.Visibility = needsConnect ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshPlaybackStateAsync()
    {
        var result = await _commands.ExecuteAsync(MusicCommand.GetPlaybackState("spotify"));
        if (!result.Succeeded || result.CurrentTrack is null)
        {
            PlaybackProgress.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateCurrentTrackUi(result.CurrentTrack, result.IsPlaying);
        if (result.DurationMilliseconds is > 0 && result.ProgressMilliseconds is not null)
        {
            PlaybackProgress.Maximum = result.DurationMilliseconds.Value;
            PlaybackProgress.Value = result.ProgressMilliseconds.Value;
            PlaybackProgress.Visibility = Visibility.Visible;
        }
        else
        {
            PlaybackProgress.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConnectSpotifyButton_Click(object sender, RoutedEventArgs e)
    {
        StatusLabel.Text = "Connecting to Spotify…";
        var result = await _commands.ExecuteAsync(MusicCommand.ConnectProvider("spotify"));
        if (!result.Succeeded)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Spotify connect failed.";
            return;
        }

        _connectDismissed = true;
        RefreshConnectPanel();
        SourceLabel.Text = DescribeSource();
        StatusLabel.Text = "Spotify connected.";
        ResultsList.Children.Clear();
        ResultsList.Children.Add(CreateMuted("Search Spotify tracks above."));
        await RefreshPlaybackStateAsync();
    }

    private void ConnectYouTubeButton_Click(object sender, RoutedEventArgs e)
    {
        StatusLabel.Text =
            "YouTube Music does not offer a full public API like Spotify. Use Spotify or the demo catalog for now.";
    }

    private void LaterConnectButton_Click(object sender, RoutedEventArgs e)
    {
        _connectDismissed = true;
        RefreshConnectPanel();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SourceLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(ConnectPromptText, theme);
        WidgetSurfaceStyle.ApplyBody(TrackTitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(TrackArtistText, theme);
        SearchBox.FontFamily = new FontFamily(theme.FontFamily);
        ArtGlyph.Foreground = ThemePainter.Brush(theme.Accent);
        ArtPlaceholder.Background = ThemePainter.Brush(theme.Accent, 0.18);
        ArtPlaceholder.CornerRadius = new CornerRadius(Math.Max(10, theme.CornerRadius * 0.55));
        WidgetSurfaceStyle.ApplyProgress(PlaybackProgress, theme);

        WidgetSurfaceStyle.ApplyActionButton(SearchButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(PreviousButton, theme);
        WidgetSurfaceStyle.ApplyActionButton(PlayPauseButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(NextButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(OpenWebSourceButton, theme);
        WidgetSurfaceStyle.ApplyActionButton(ConnectSpotifyButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ConnectYouTubeButton, theme);
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = RunSearchAsync();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => _ = RunSearchAsync();

    private async Task RunSearchAsync()
    {
        if (Interlocked.Exchange(ref _searchGate, 1) == 1)
        {
            return;
        }

        try
        {
            StatusLabel.Text = "Searching…";
            ResultsList.Children.Clear();
            ResultsList.Children.Add(CreateMuted("Searching…"));

            var result = await _commands.ExecuteAsync(MusicCommand.SearchTrack(SearchBox.Text ?? string.Empty));
            ResultsList.Children.Clear();
            _lastResults.Clear();

            if (!result.Succeeded)
            {
                ResultsList.Children.Add(CreateMuted(result.ErrorMessage ?? "Search failed."));
                StatusLabel.Text = result.ErrorMessage ?? "Search failed.";
                return;
            }

            if (result.Tracks.Count == 0)
            {
                ResultsList.Children.Add(CreateMuted("No tracks found."));
                StatusLabel.Text = "No tracks found.";
                return;
            }

            _lastResults.AddRange(result.Tracks);
            foreach (var track in result.Tracks)
            {
                ResultsList.Children.Add(CreateResultRow(track));
            }

            StatusLabel.Text = $"{result.Tracks.Count} result(s) · Demo catalog";
            UpdateTransportEnabled();
        }
        finally
        {
            Interlocked.Exchange(ref _searchGate, 0);
        }
    }

    private UIElement CreateResultRow(MusicTrack track)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Tag = track.Id
        };

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = track.Title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{track.Artist} · {track.Source}",
            FontSize = 11,
            Opacity = 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        button.Content = panel;
        button.Click += ResultRow_Click;

        if (_theme is not null)
        {
            StyleActionButton(button, _theme);
            if (button.Content is StackPanel sp)
            {
                foreach (var child in sp.Children.OfType<TextBlock>())
                {
                    child.FontFamily = new FontFamily(_theme.FontFamily);
                    child.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
                }
            }
        }

        return button;
    }

    private async void ResultRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        var track = _lastResults.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
        if (track is null)
        {
            return;
        }

        await RunCommandAsync(MusicCommand.PlayTrack(track));
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        var playback = _musicService.GetPlaybackProvider();
        if (playback?.CurrentTrack is null && _configuration.CurrentTrack is not null)
        {
            await RunCommandAsync(MusicCommand.PlayTrack(_configuration.CurrentTrack));
            return;
        }

        if (playback is { IsPlaying: true })
        {
            await RunCommandAsync(MusicCommand.Pause());
        }
        else
        {
            await RunCommandAsync(MusicCommand.Resume());
        }
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e) =>
        await RunCommandAsync(MusicCommand.Previous());

    private async void NextButton_Click(object sender, RoutedEventArgs e) =>
        await RunCommandAsync(MusicCommand.Next());

    private async Task RunCommandAsync(MusicCommand command)
    {
        if (Interlocked.Exchange(ref _commandGate, 1) == 1)
        {
            return;
        }

        try
        {
            var result = await _commands.ExecuteAsync(command);
            if (!result.Succeeded)
            {
                StatusLabel.Text = result.ErrorMessage ?? "Command failed.";
                UpdateTransportEnabled();
                return;
            }

            if (result.CurrentTrack is not null)
            {
                _configuration.CurrentTrack = result.CurrentTrack;
                Persist();
            }

            UpdateCurrentTrackUi(result.CurrentTrack ?? _configuration.CurrentTrack, result.IsPlaying);
            StatusLabel.Text = result.Kind switch
            {
                MusicCommandKind.PlayTrack => "Playing",
                MusicCommandKind.Pause => "Paused",
                MusicCommandKind.Resume => "Playing",
                MusicCommandKind.Next => "Next",
                MusicCommandKind.Previous => "Previous",
                _ => StatusLabel.Text
            };
            UpdateTransportEnabled();
        }
        finally
        {
            Interlocked.Exchange(ref _commandGate, 0);
        }
    }

    private void UpdateCurrentTrackUi(MusicTrack? track, bool isPlaying)
    {
        if (track is null || string.IsNullOrWhiteSpace(track.Title))
        {
            TrackTitleText.Text = "No track selected";
            TrackArtistText.Text = "Search and pick a track";
            SourceLabel.Text = "Source: Demo catalog";
            PlayPauseButton.Content = "▶";
            return;
        }

        TrackTitleText.Text = track.Title;
        TrackArtistText.Text = string.IsNullOrWhiteSpace(track.Artist) ? track.Source : track.Artist;
        SourceLabel.Text = string.IsNullOrWhiteSpace(track.Source)
            ? $"Source: {track.ProviderId}"
            : $"Source: {track.Source}";
        PlayPauseButton.Content = isPlaying ? "⏸" : "▶";
    }

    private void UpdateTransportEnabled()
    {
        var caps = _musicService.AggregateCapabilities();
        var hasTrack = _configuration.CurrentTrack is not null
                       || _musicService.GetPlaybackProvider()?.CurrentTrack is not null;

        SearchButton.IsEnabled = caps.HasFlag(MusicProviderCapabilities.Search);
        PreviousButton.IsEnabled = caps.HasFlag(MusicProviderCapabilities.Previous) && hasTrack;
        NextButton.IsEnabled = caps.HasFlag(MusicProviderCapabilities.Next) && hasTrack;
        PlayPauseButton.IsEnabled = hasTrack && (
            caps.HasFlag(MusicProviderCapabilities.Playback)
            || caps.HasFlag(MusicProviderCapabilities.Pause)
            || caps.HasFlag(MusicProviderCapabilities.Resume));
    }

    private async void OpenWebSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var sources = _configuration.Sources.Where(s => s.IsEnabled && s.Type != MusicSourceType.Local).ToList();
        if (sources.Count == 0)
        {
            StatusLabel.Text = "No web music sources configured.";
            return;
        }

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 220
        };
        foreach (var s in sources)
        {
            list.Items.Add($"{s.Name} ({s.Type})");
        }

        list.SelectedIndex = 0;
        var dialog = new ContentDialog
        {
            Title = "Open web source",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Opens in the system browser (https only). This is optional — Music UI stays native.",
                        FontSize = 12,
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    list
                }
            },
            PrimaryButtonText = "Open",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary || list.SelectedIndex < 0)
        {
            return;
        }

        var source = sources[list.SelectedIndex];
        if (!_musicService.TryResolveOpenUrl(source, out var url, out var error) || url is null)
        {
            StatusLabel.Text = error ?? "Cannot open source.";
            return;
        }

        if (_openUrl?.Invoke(url) == true)
        {
            StatusLabel.Text = $"Opened {source.Name} in browser.";
        }
        else
        {
            StatusLabel.Text = "Could not open browser.";
        }
    }

    private TextBlock CreateMuted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 8, 0, 8)
        };
        if (_theme is not null)
        {
            block.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            block.FontFamily = new FontFamily(_theme.FontFamily);
        }

        return block;
    }

    private void Persist() => _onConfigurationChanged?.Invoke(_configuration);
}
