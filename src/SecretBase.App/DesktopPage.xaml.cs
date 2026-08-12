using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.App.Desktop;
using SecretBase.Core;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Text;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Clock;
using SecretBase.Widgets.Text;
using SecretBase.Widgets.Theming;

namespace SecretBase.App;

public sealed partial class DesktopPage : Page
{
    private IAppLogger? _logger;
    private ISafeExitService? _safeExit;
    private ILayoutStore? _layoutStore;
    private IThemeStore? _themeStore;
    private ITimeProvider? _timeProvider;
    private DesktopLayout? _layout;
    private ThemeDefinition? _theme;
    private CompatibilityInfo? _compatibility;
    private readonly List<IDisposable> _widgetDisposables = [];
    private bool _debugChromeVisible;

    public DesktopPage()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not DesktopPageArgs args)
        {
            ShowDebugChrome(forceVisible: true);
            StatusText.Text = "Desktop failed to start: missing bootstrap args.";
            return;
        }

        _logger = args.Logger;
        _safeExit = args.SafeExit;
        _layoutStore = args.LayoutStore;
        _themeStore = args.ThemeStore;
        _timeProvider = args.TimeProvider;
        _compatibility = args.Compatibility;

        _theme = _themeStore.LoadOrCreateDefault();
        _layout = _layoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);
        EnsureSeedTextWidget(_layout);

        ApplyDesktopTheme(_theme);
        RefreshDebugStatus();
        // Normal UX: widgets only — no title / status / Exit chrome on the wallpaper.
        ShowDebugChrome(forceVisible: false);

        RenderWidgets();
        _logger.Info("desktop", $"Overlay desktop shown for room '{_layout.RoomId}' with {_layout.Widgets.Count} widget(s).");
        _logger.Info("widget", "Clock and Text widget hosts ready.");
        _logger.Info("overlay", "Widgets-only overlay UX (debug chrome hidden; Ctrl+Shift+Q exit, Ctrl+Shift+D toggle debug).");
    }

    private void ApplyDesktopTheme(ThemeDefinition theme)
    {
        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        WidgetCanvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        StatusText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusText.FontFamily = new FontFamily(theme.FontFamily);
        DebugChrome.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        DebugChrome.CornerRadius = new CornerRadius(Math.Max(8, theme.CornerRadius / 2));
    }

    private void RefreshDebugStatus()
    {
        if (_layout is null || _compatibility is null)
        {
            return;
        }

        StatusText.Text =
            $"{AppInfo.Name} · {_layout.Widgets.Count} widgets · v{_compatibility.AppVersion} · Ctrl+Shift+Q exit";
    }

    private void ShowDebugChrome(bool forceVisible)
    {
        _debugChromeVisible = forceVisible;
        DebugChrome.Visibility = forceVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleDebugChrome()
    {
        ShowDebugChrome(!_debugChromeVisible);
        if (_debugChromeVisible)
        {
            RefreshDebugStatus();
            _logger?.Info("overlay", "Debug chrome shown (Ctrl+Shift+D).");
        }
        else
        {
            _logger?.Info("overlay", "Debug chrome hidden (Ctrl+Shift+D).");
        }
    }

    private void RenderWidgets()
    {
        DisposeWidgets();
        WidgetCanvas.Children.Clear();
        if (_layout is null || _theme is null)
        {
            return;
        }

        foreach (var instance in _layout.Widgets)
        {
            instance.Size.Clamp(_theme.WidgetMinWidth, _theme.WidgetMinHeight);
            var content = CreateWidgetContent(instance);
            if (content is null)
            {
                _logger?.Warn("widget", $"Unsupported widget type '{instance.Type}' — skipped.");
                continue;
            }

            var frame = new WidgetFrame(instance, content, _theme, PersistLayoutNow);
            Canvas.SetLeft(frame, instance.Position.X);
            Canvas.SetTop(frame, instance.Position.Y);
            WidgetCanvas.Children.Add(frame);
        }
    }

    private UIElement? CreateWidgetContent(WidgetInstance instance)
    {
        if (instance.Type == WidgetTypes.Clock)
        {
            var config = ClockWidgetConfiguration.FromDictionary(instance.Configuration);
            config.Use24HourFormat = true;
            instance.Configuration = config.ToDictionary();

            var view = new ClockWidgetView();
            view.Initialize(config, _timeProvider);
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            _widgetDisposables.Add(view);
            return view;
        }

        if (instance.Type == WidgetTypes.Text)
        {
            var config = TextWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var view = new TextWidgetView();
            view.Initialize(config, updated =>
            {
                instance.Configuration = updated.ToDictionary();
                PersistLayoutNow();
            });
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        return null;
    }

    private void EnsureSeedTextWidget(DesktopLayout layout)
    {
        if (layout.Widgets.Any(w => string.Equals(w.Type, WidgetTypes.Text, StringComparison.Ordinal)))
        {
            return;
        }

        layout.Widgets.Add(DefaultWidgetFactory.CreateDefaultText(layout.RoomId));
        try
        {
            _layoutStore?.Save(layout);
            _logger?.Info("widget", "Seeded default Text widget (no Add Widget UI yet).");
        }
        catch (Exception ex)
        {
            _logger?.Error("persistence", "Failed to save layout after seeding Text widget.", ex);
        }
    }

    private void PersistLayoutNow()
    {
        if (_layout is null || _layoutStore is null)
        {
            return;
        }

        try
        {
            _layoutStore.Save(_layout);
            _logger?.Info("persistence", $"Layout saved ({_layout.Widgets.Count} widgets).");
        }
        catch (Exception ex)
        {
            _logger?.Error("persistence", "Failed to save layout.", ex);
        }
    }

    private void RequestSafeExit(string reason)
    {
        PersistLayoutNow();
        _logger?.Info("desktop", reason);
        _safeExit?.RequestExit();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) =>
        RequestSafeExit("User chose Exit from debug chrome.");

    private void ExitAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        RequestSafeExit("User requested Safe Exit via Ctrl+Shift+Q.");
    }

    private void DebugChromeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ToggleDebugChrome();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PersistLayoutNow();
        DisposeWidgets();
    }

    private void DisposeWidgets()
    {
        foreach (var disposable in _widgetDisposables)
        {
            disposable.Dispose();
        }

        _widgetDisposables.Clear();
    }
}
