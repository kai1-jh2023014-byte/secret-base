using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.App.Desktop;
using SecretBase.Core;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Clock;
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
    private readonly List<IDisposable> _widgetDisposables = [];

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
            StatusText.Text = "Desktop failed to start: missing bootstrap args.";
            return;
        }

        _logger = args.Logger;
        _safeExit = args.SafeExit;
        _layoutStore = args.LayoutStore;
        _themeStore = args.ThemeStore;
        _timeProvider = args.TimeProvider;

        _theme = _themeStore.LoadOrCreateDefault();
        _layout = _layoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);

        ApplyDesktopTheme(_theme);
        BrandText.Text = AppInfo.Name;
        StatusText.Text =
            $"Room: {_layout.RoomId} · Widgets: {_layout.Widgets.Count} · " +
            $"App {args.Compatibility.AppVersion}";

        RenderWidgets();
        _logger.Info("desktop", $"Desktop shown for room '{_layout.RoomId}' with {_layout.Widgets.Count} widget(s).");
        _logger.Info("widget", "Clock widget host ready.");
    }

    private void ApplyDesktopTheme(ThemeDefinition theme)
    {
        RootGrid.Background = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = ThemePainter.ParseColor(theme.Background), Offset = 0 },
                new GradientStop { Color = ThemePainter.ParseColor(theme.BackgroundSecondary), Offset = 1 }
            }
        };
        BrandText.Foreground = ThemePainter.Brush(theme.Foreground);
        StatusText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        BrandText.FontFamily = new FontFamily(theme.FontFamily);
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

        return null;
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

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        PersistLayoutNow();
        _logger?.Info("desktop", "User chose Exit to Windows Desktop.");
        _safeExit?.RequestExit();
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
