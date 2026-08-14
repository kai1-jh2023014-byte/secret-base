using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using SecretBase.Core.Music;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Web;
using SecretBase.Widgets.Theming;
using Windows.UI;

namespace SecretBase.Widgets.Music;

/// <summary>
/// Music Hub widget. Sources are first-class; browsing uses Untrusted WebView2
/// with the same harden rules as Web Widget (no host bridge).
/// </summary>
public sealed partial class MusicWidgetView : UserControl
{
    private MusicWidgetConfiguration _configuration = MusicWidgetConfiguration.CreateDefault();
    private readonly MusicService _musicService = new();
    private Action<MusicWidgetConfiguration>? _onConfigurationChanged;
    private ThemeDefinition? _theme;
    private bool _coreReady;
    private string? _pendingNavigateUrl;
    private string? _blockedMessage;
    private bool _browseMode;

    public MusicWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        MusicWidgetConfiguration configuration,
        Action<MusicWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _onConfigurationChanged = onConfigurationChanged;
        ShowHub();
        RebuildSourceList();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        HeaderText.FontFamily = font;
        HeaderText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        HeaderSubText.FontFamily = font;
        HeaderSubText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        HubHint.FontFamily = font;
        HubHint.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusText.FontFamily = font;
        StatusText.Foreground = ThemePainter.Brush(theme.WidgetForeground);

        HeaderBar.Background = ThemePainter.Brush(theme.WidgetBackground, Math.Min(1.0, ThemePainter.EffectiveWidgetOpacity(theme) + 0.06));
        StyleActionButton(BackButton, theme);
        StyleActionButton(AddSourceButton, theme);
        RebuildSourceList();
    }

    private static void StyleActionButton(Button button, ThemeDefinition theme)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Accent, 0.55);
        button.BorderThickness = new Thickness(1);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureBrowserAsync();
            if (!string.IsNullOrEmpty(_pendingNavigateUrl))
            {
                NavigateTo(_pendingNavigateUrl!);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"WebView2 failed to start: {ex.Message}", loading: false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachBrowserHandlers();

    private async Task EnsureBrowserAsync()
    {
        if (_coreReady)
        {
            return;
        }

        await Browser.EnsureCoreWebView2Async();
        HardenWebView();
        AttachBrowserHandlers();
        _coreReady = true;
    }

    private void HardenWebView()
    {
        var core = Browser.CoreWebView2;
        if (core is null)
        {
            return;
        }

        var settings = core.Settings;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = true;
        settings.AreDevToolsEnabled = false;
        Browser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);
    }

    private void AttachBrowserHandlers()
    {
        var core = Browser.CoreWebView2;
        if (core is null)
        {
            return;
        }

        core.NavigationStarting += Core_NavigationStarting;
        core.NavigationCompleted += Core_NavigationCompleted;
        core.NewWindowRequested += Core_NewWindowRequested;
    }

    private void DetachBrowserHandlers()
    {
        var core = Browser.CoreWebView2;
        if (core is null)
        {
            return;
        }

        core.NavigationStarting -= Core_NavigationStarting;
        core.NavigationCompleted -= Core_NavigationCompleted;
        core.NewWindowRequested -= Core_NewWindowRequested;
    }

    private void Core_NavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!WebUrlValidator.TryNormalize(args.Uri, out _, out var error))
        {
            args.Cancel = true;
            _blockedMessage = error ?? WebUrlValidator.BlockedMessage;
            ShowStatus(_blockedMessage, loading: false);
            return;
        }

        _blockedMessage = null;
        ShowStatus("Loading…", loading: true);
    }

    private void Core_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!string.IsNullOrEmpty(_blockedMessage))
        {
            ShowStatus(_blockedMessage, loading: false);
            return;
        }

        if (!args.IsSuccess)
        {
            ShowStatus($"Failed to load ({args.WebErrorStatus}).", loading: false);
            return;
        }

        HideStatus();
    }

    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        if (WebUrlValidator.TryNormalize(args.Uri, out var normalized, out var error))
        {
            NavigateTo(normalized!);
        }
        else
        {
            ShowStatus(error ?? WebUrlValidator.BlockedMessage, loading: false);
        }
    }

    private void ShowHub()
    {
        _browseMode = false;
        HubPanel.Visibility = Visibility.Visible;
        BrowsePanel.Visibility = Visibility.Collapsed;
        BackButton.Visibility = Visibility.Collapsed;
        HeaderText.Text = "Music";
        HeaderSubText.Text = "Sources";
        HideStatus();
    }

    private void ShowBrowse(string title)
    {
        _browseMode = true;
        HubPanel.Visibility = Visibility.Collapsed;
        BrowsePanel.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Visible;
        HeaderText.Text = title;
        HeaderSubText.Text = "Untrusted";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowHub();

    private void RebuildSourceList()
    {
        SourceList.Children.Clear();
        var enabled = _configuration.Sources.Where(s => s.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            SourceList.Children.Add(CreateMuted("No music selected"));
            return;
        }

        foreach (var source in enabled)
        {
            SourceList.Children.Add(CreateSourceRow(source));
        }
    }

    private UIElement CreateSourceRow(MusicSource source)
    {
        var open = new Button
        {
            Content = source.Name,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 10, 12, 10),
            Tag = source.Id
        };
        open.Click += SourceOpen_Click;

        var typeLabel = new TextBlock
        {
            Text = source.Type.ToString(),
            FontSize = 11,
            Opacity = 0.7,
            Margin = new Thickness(0, 2, 0, 0)
        };

        if (_theme is not null)
        {
            StyleActionButton(open, _theme);
            open.BorderBrush = ThemePainter.Brush(_theme.Accent, 0.45);
            typeLabel.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            typeLabel.FontFamily = new FontFamily(_theme.FontFamily);
        }

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(open);
        panel.Children.Add(typeLabel);
        return panel;
    }

    private TextBlock CreateMuted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Opacity = 0.8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 24),
            TextWrapping = TextWrapping.WrapWholeWords
        };
        if (_theme is not null)
        {
            block.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            block.FontFamily = new FontFamily(_theme.FontFamily);
        }

        return block;
    }

    private void SourceOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string id })
        {
            return;
        }

        var source = _configuration.FindSource(id);
        if (source is null)
        {
            return;
        }

        OpenSource(source);
    }

    private async void OpenSource(MusicSource source)
    {
        _configuration.ActiveSourceId = source.Id;
        Persist();

        if (!_musicService.TryResolveOpenUrl(source, out var url, out var error))
        {
            ShowBrowse(source.Name);
            ShowStatus(error ?? "Cannot open this source.", loading: false);
            return;
        }

        ShowBrowse(source.Name);
        try
        {
            await EnsureBrowserAsync();
            NavigateTo(url!);
        }
        catch (Exception ex)
        {
            ShowStatus($"WebView2 failed: {ex.Message}", loading: false);
        }
    }

    private void NavigateTo(string url)
    {
        if (!_coreReady || Browser.CoreWebView2 is null)
        {
            _pendingNavigateUrl = url;
            ShowStatus("Loading…", loading: true);
            return;
        }

        _pendingNavigateUrl = null;
        if (!WebUrlValidator.TryNormalize(url, out var normalized, out var error))
        {
            ShowStatus(error ?? WebUrlValidator.BlockedMessage, loading: false);
            return;
        }

        ShowStatus("Loading…", loading: true);
        Browser.CoreWebView2.Navigate(normalized);
    }

    private async void AddSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var typeBox = new ComboBox
        {
            Header = "Type",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        typeBox.Items.Add(MusicSourceType.Spotify.ToString());
        typeBox.Items.Add(MusicSourceType.YouTube.ToString());
        typeBox.Items.Add(MusicSourceType.Web.ToString());
        typeBox.Items.Add(MusicSourceType.Local.ToString());
        typeBox.SelectedIndex = 0;

        var nameBox = new TextBox { Header = "Name", Text = "Spotify" };
        var urlBox = new TextBox
        {
            Header = "URL (https)",
            Text = MusicWidgetConfiguration.DefaultSpotifyUrl,
            PlaceholderText = "https://"
        };

        typeBox.SelectionChanged += (_, _) =>
        {
            var selected = typeBox.SelectedItem?.ToString();
            if (string.Equals(selected, nameof(MusicSourceType.Spotify), StringComparison.Ordinal))
            {
                nameBox.Text = "Spotify";
                urlBox.Text = MusicWidgetConfiguration.DefaultSpotifyUrl;
                urlBox.IsEnabled = true;
            }
            else if (string.Equals(selected, nameof(MusicSourceType.YouTube), StringComparison.Ordinal))
            {
                nameBox.Text = "YouTube Music";
                urlBox.Text = MusicWidgetConfiguration.DefaultYouTubeUrl;
                urlBox.IsEnabled = true;
            }
            else if (string.Equals(selected, nameof(MusicSourceType.Local), StringComparison.Ordinal))
            {
                nameBox.Text = "Local";
                urlBox.Text = string.Empty;
                urlBox.IsEnabled = false;
            }
            else
            {
                nameBox.Text = "Web";
                urlBox.Text = "https://";
                urlBox.IsEnabled = true;
            }
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Registers a music source (metadata only). No OAuth tokens or cookies are stored.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(typeBox);
        panel.Children.Add(nameBox);
        panel.Children.Add(urlBox);

        var dialog = new ContentDialog
        {
            Title = "Add music source",
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (!Enum.TryParse<MusicSourceType>(typeBox.SelectedItem?.ToString(), out var type))
        {
            return;
        }

        var name = nameBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        string? url = null;
        if (type != MusicSourceType.Local)
        {
            if (!WebUrlValidator.TryNormalize(urlBox.Text, out var normalized, out _))
            {
                ShowHub();
                HubHint.Text = WebUrlValidator.BlockedMessage;
                return;
            }

            url = normalized;
        }

        _configuration.Sources.Add(new MusicSource
        {
            Id = Guid.NewGuid().ToString("N"),
            Type = type,
            Name = name,
            Url = url,
            IsEnabled = true
        });
        Persist();
        RebuildSourceList();
        HubHint.Text = "Pick a source to open it here. Web content stays Untrusted — no Host Bridge.";
    }

    private void Persist() => _onConfigurationChanged?.Invoke(_configuration);

    private void ShowStatus(string message, bool loading)
    {
        if (!_browseMode)
        {
            HubHint.Text = message;
            return;
        }

        StatusOverlay.Visibility = Visibility.Visible;
        StatusText.Text = message;
        LoadingRing.IsActive = loading;
        LoadingRing.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HideStatus()
    {
        StatusOverlay.Visibility = Visibility.Collapsed;
        LoadingRing.IsActive = false;
    }
}
