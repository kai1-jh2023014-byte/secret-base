using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Web;
using SecretBase.Widgets.Theming;
using Windows.System;
using Windows.UI;

namespace SecretBase.Widgets.Web;

/// <summary>
/// Untrusted WebView2 host. Displays http(s) pages only.
/// No host object bridge, no Core/Platform injection, no JS→Secret Base API.
/// Move/resize stay on <c>WidgetFrame</c> chrome (not this surface).
/// </summary>
public sealed partial class WebWidgetView : UserControl, IDisposable
{
    private WebWidgetConfiguration _configuration = WebWidgetConfiguration.CreateDefault();
    private Action<WebWidgetConfiguration>? _onConfigurationChanged;
    private bool _coreReady;
    private bool _navigatePending;
    private string? _blockedMessage;
    private bool _disposed;

    public WebWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        WebWidgetConfiguration configuration,
        Action<WebWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _onConfigurationChanged = onConfigurationChanged;
        UrlBox.Text = _configuration.Url;
        _navigatePending = true;
        if (_coreReady)
        {
            NavigateToConfiguredUrl();
        }
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        UrlBox.FontFamily = font;
        StatusText.FontFamily = font;
        StatusText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        GoButton.FontFamily = font;
        ReloadButton.FontFamily = font;

        Toolbar.Background = ThemePainter.Brush(theme.WidgetBackground, Math.Min(1.0, ThemePainter.EffectiveWidgetOpacity(theme) + 0.08));
        GoButton.Background = ThemePainter.Brush(theme.Accent, 0.85);
        GoButton.Foreground = ThemePainter.Brush(theme.Foreground);
        ReloadButton.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        ReloadButton.Foreground = ThemePainter.Brush(theme.WidgetForeground);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureBrowserAsync();
            if (_navigatePending)
            {
                NavigateToConfiguredUrl();
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"WebView2 failed to start: {ex.Message}", loading: false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DisposeBrowser();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        DisposeBrowser();
    }

    private void DisposeBrowser()
    {
        DetachBrowserHandlers();
        try
        {
            if (Browser.CoreWebView2 is not null)
            {
                Browser.CoreWebView2.Stop();
            }

            Browser.Close();
        }
        catch
        {
            // Best-effort teardown — avoid throwing during unload/re-render.
        }

        _coreReady = false;
    }

    private async Task EnsureBrowserAsync()
    {
        if (_coreReady)
        {
            return;
        }

        ShowStatus("Loading…", loading: true);
        await Browser.EnsureCoreWebView2Async();
        HardenWebView();
        AttachBrowserHandlers();
        _coreReady = true;
        HideStatus();
    }

    /// <summary>
    /// Hardens CoreWebView2: no host objects, no web messaging bridge.
    /// </summary>
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

        // Transparent page backdrop so theme chrome shows around content edges.
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
        if (!WebUrlValidator.TryNormalize(args.Uri, out var normalized, out var error))
        {
            args.Cancel = true;
            _blockedMessage = error ?? WebUrlValidator.BlockedMessage;
            ShowStatus(_blockedMessage, loading: false);
            return;
        }

        _blockedMessage = null;
        UrlBox.Text = normalized;
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
            ShowStatus($"Failed to load page ({args.WebErrorStatus}).", loading: false);
            return;
        }

        HideStatus();
        var current = sender.Source;
        if (WebUrlValidator.TryNormalize(current, out var normalized, out _))
        {
            UrlBox.Text = normalized;
            if (!string.Equals(_configuration.Url, normalized, StringComparison.Ordinal))
            {
                _configuration.Url = normalized!;
                _onConfigurationChanged?.Invoke(_configuration);
            }
        }
    }

    private void Core_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        // Keep navigation inside this widget — no OS browser / new HWND.
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

    private void NavigateToConfiguredUrl()
    {
        _navigatePending = false;
        NavigateTo(_configuration.Url);
    }

    private void NavigateTo(string url)
    {
        if (!_coreReady || Browser.CoreWebView2 is null)
        {
            _navigatePending = true;
            _configuration.Url = url;
            return;
        }

        if (!WebUrlValidator.TryNormalize(url, out var normalized, out var error))
        {
            ShowStatus(error ?? WebUrlValidator.BlockedMessage, loading: false);
            return;
        }

        _configuration.Url = normalized!;
        UrlBox.Text = normalized;
        ShowStatus("Loading…", loading: true);
        Browser.CoreWebView2.Navigate(normalized);
    }

    private void CommitUrlFromBox(bool persist)
    {
        var raw = UrlBox.Text;
        if (!WebUrlValidator.TryNormalize(raw, out var normalized, out var error))
        {
            ShowStatus(error ?? WebUrlValidator.BlockedMessage, loading: false);
            return;
        }

        _configuration.Url = normalized!;
        if (persist)
        {
            _onConfigurationChanged?.Invoke(_configuration);
        }

        NavigateTo(normalized!);
    }

    private void GoButton_Click(object sender, RoutedEventArgs e) => CommitUrlFromBox(persist: true);

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady && Browser.CoreWebView2 is not null && string.IsNullOrEmpty(_blockedMessage))
        {
            ShowStatus("Loading…", loading: true);
            Browser.CoreWebView2.Reload();
            return;
        }

        CommitUrlFromBox(persist: false);
    }

    private void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitUrlFromBox(persist: true);
            e.Handled = true;
        }
    }

    private void ShowStatus(string message, bool loading)
    {
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
