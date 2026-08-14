using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.App.Desktop;
using SecretBase.Core;
using SecretBase.Core.Blocks;
using SecretBase.Core.Calendar;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Creative;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Text;
using SecretBase.Core.Widgets.Web;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Windows;
using SecretBase.Widgets.Calendar;
using SecretBase.Widgets.Clock;
using SecretBase.Widgets.Creative;
using SecretBase.Widgets.Music;
using SecretBase.Widgets.Text;
using SecretBase.Widgets.Theming;
using SecretBase.Widgets.Web;
using SecretBase.Core.Blocks;
using SecretBase.Core.Creative;
using Windows.Foundation;

namespace SecretBase.App;

public sealed partial class DesktopPage : Page
{
    private IAppLogger? _logger;
    private ISafeExitService? _safeExit;
    private ILayoutStore? _layoutStore;
    private IThemeStore? _themeStore;
    private ITimeProvider? _timeProvider;
    private IDesktopOverlayService? _overlay;
    private DesktopOverlayTarget? _overlayTarget;
    private ITargetLaunchService? _launcher;
    private IFileIconService? _icons;
    private IBlockItemIntakeService? _intake;
    private ISecureSecretStore? _secretStore;
    private ICalendarAgendaCache? _calendarCache;
    private IPathPickService? _pathPicker;
    private CreativeCommandService? _creativeCommands;
    private DesktopLayout? _layout;
    private ThemeDefinition? _theme;
    private CompatibilityInfo? _compatibility;
    private readonly List<IDisposable> _widgetDisposables = [];
    private bool _debugChromeVisible;

    public DesktopPage()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => SyncInteractiveInputRegions();
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
        _overlay = args.Overlay;
        _overlayTarget = args.OverlayTarget;
        _launcher = args.Launcher;
        _icons = args.Icons;
        _intake = args.Intake;
        _secretStore = args.SecretStore ?? new WindowsCredentialSecretStore();
        _calendarCache = args.CalendarCache ?? new JsonCalendarAgendaCache();
        _pathPicker = args.PathPicker;
        _creativeCommands = args.CreativeCommands
            ?? new CreativeCommandService(
                new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()));

        _theme = _themeStore.LoadOrCreateDefault();
        _layout = _layoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);
        EnsureSeedTextWidget(_layout);

        ApplyDesktopTheme(_theme);
        StyleFabButtons(_theme);
        RefreshDebugStatus();
        ShowDebugChrome(forceVisible: false);

        RenderDesktopObjects();
        _logger.Info("desktop", $"Overlay desktop shown for room '{_layout.RoomId}' with {_layout.Widgets.Count} widget(s), {_layout.Blocks.Count} block(s).");
        _logger.Info("widget", "Clock and Text widget hosts ready.");
        _logger.Info("block", "Block host ready (use + button to add; drop + drag icons inside a Block).");
        _logger.Info("theme", "Theme editor ready (Aa button) — colors apply to Clock, Text, and Blocks.");
        _logger.Info("layout", "Arrange ready (Grid button) — even placement for widgets and blocks.");
        _logger.Info("widget", "Web Widget ready (Web button) — Untrusted WebView2, no host bridge.");
        _logger.Info("widget", "Calendar Hub ready (Cal button) — Local / Mock / Google ICS / Google API read.");
        _logger.Info("widget", "Music Widget ready (♪ button) — native search/playback UI via MusicCommand; Demo catalog; no Host Bridge.");
        _logger.Info("widget", "Creative Workspace ready (CW button) — favorites/recent/open registered paths only.");
    }

    private void StyleFabButtons(ThemeDefinition theme)
    {
        AddBlockFab.Background = ThemePainter.Brush(theme.Accent, 0.92);
        AddBlockFab.Foreground = ThemePainter.Brush(theme.Foreground);
        AddBlockFab.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.35);
        AddBlockFab.BorderThickness = new Thickness(1);

        AddWebFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        AddWebFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        AddWebFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        AddWebFab.BorderThickness = new Thickness(1);
        AddWebFab.FontFamily = new FontFamily(theme.FontFamily);

        AddCalendarFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        AddCalendarFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        AddCalendarFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        AddCalendarFab.BorderThickness = new Thickness(1);
        AddCalendarFab.FontFamily = new FontFamily(theme.FontFamily);

        AddMusicFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        AddMusicFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        AddMusicFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        AddMusicFab.BorderThickness = new Thickness(1);
        AddMusicFab.FontFamily = new FontFamily(theme.FontFamily);

        AddCreativeFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        AddCreativeFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        AddCreativeFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        AddCreativeFab.BorderThickness = new Thickness(1);
        AddCreativeFab.FontFamily = new FontFamily(theme.FontFamily);

        ThemeFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        ThemeFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        ThemeFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        ThemeFab.BorderThickness = new Thickness(1);
        ThemeFab.FontFamily = new FontFamily(theme.FontFamily);

        ArrangeFab.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        ArrangeFab.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        ArrangeFab.BorderBrush = ThemePainter.Brush(theme.Accent, 0.7);
        ArrangeFab.BorderThickness = new Thickness(1);
        ArrangeFab.FontFamily = new FontFamily(theme.FontFamily);
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
            $"{AppInfo.Name} · {_layout.Widgets.Count}w / {_layout.Blocks.Count}b · v{_compatibility.AppVersion}";
    }

    private void ShowDebugChrome(bool forceVisible)
    {
        _debugChromeVisible = forceVisible;
        DebugChrome.Visibility = forceVisible ? Visibility.Visible : Visibility.Collapsed;
        SyncInteractiveInputRegions();
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

    private void RenderDesktopObjects()
    {
        DisposeWidgets();
        WidgetCanvas.Children.Clear();
        if (_layout is null || _theme is null)
        {
            SyncInteractiveInputRegions();
            return;
        }

        RenderWidgets();
        RenderBlocks();
        SyncInteractiveInputRegions();
    }

    private void RenderWidgets()
    {
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

            var frame = new WidgetFrame(
                instance,
                content,
                _theme,
                onLayoutCommitted: PersistLayoutNow,
                onBoundsChanged: SyncInteractiveInputRegions);
            Canvas.SetLeft(frame, instance.Position.X);
            Canvas.SetTop(frame, instance.Position.Y);
            frame.Loaded += (_, _) => SyncInteractiveInputRegions();
            WidgetCanvas.Children.Add(frame);
        }
    }

    private void RenderBlocks()
    {
        if (_layout is null || _theme is null || _launcher is null || _icons is null || _intake is null)
        {
            return;
        }

        foreach (var block in _layout.Blocks)
        {
            block.ClampSize();
            var frame = new BlockFrame(
                block,
                _theme,
                _launcher,
                _icons,
                _intake,
                onLayoutCommitted: PersistLayoutNow,
                onDeleteRequested: DeleteBlock,
                onBoundsChanged: SyncInteractiveInputRegions,
                onStatus: message =>
                {
                    _logger?.Info("block", message);
                    if (_debugChromeVisible)
                    {
                        StatusText.Text = message;
                    }
                });
            Canvas.SetLeft(frame, block.Position.X);
            Canvas.SetTop(frame, block.Position.Y);
            frame.Loaded += (_, _) => SyncInteractiveInputRegions();
            WidgetCanvas.Children.Add(frame);
        }
    }

    private void SyncInteractiveInputRegions()
    {
        if (_overlay is null || _overlayTarget is null || XamlRoot is null)
        {
            return;
        }

        var scale = XamlRoot.RasterizationScale;
        var rects = new List<OverlayInputRect>();

        foreach (var child in WidgetCanvas.Children.OfType<FrameworkElement>())
        {
            if (TryCreateClientRect(child, scale, out var rect))
            {
                rects.Add(rect);
            }
        }

        if (_debugChromeVisible && DebugChrome.Visibility == Visibility.Visible
            && TryCreateClientRect(DebugChrome, scale, out var chromeRect))
        {
            rects.Add(chromeRect);
        }

        if (TryCreateClientRect(AddBlockFab, scale, out var fabRect))
        {
            rects.Add(fabRect);
        }

        if (TryCreateClientRect(AddWebFab, scale, out var webFabRect))
        {
            rects.Add(webFabRect);
        }

        if (TryCreateClientRect(AddCalendarFab, scale, out var calendarFabRect))
        {
            rects.Add(calendarFabRect);
        }

        if (TryCreateClientRect(AddMusicFab, scale, out var musicFabRect))
        {
            rects.Add(musicFabRect);
        }

        if (TryCreateClientRect(AddCreativeFab, scale, out var creativeFabRect))
        {
            rects.Add(creativeFabRect);
        }

        if (TryCreateClientRect(ThemeFab, scale, out var themeFabRect))
        {
            rects.Add(themeFabRect);
        }

        if (TryCreateClientRect(ArrangeFab, scale, out var arrangeFabRect))
        {
            rects.Add(arrangeFabRect);
        }

        _overlay.UpdateInteractiveInputRegions(_overlayTarget, rects);
    }

    /// <summary>
    /// Called from <see cref="MainWindow"/> after XAML island creation to re-apply SetWindowRgn.
    /// </summary>
    public void RequestInteractiveRegionSync()
    {
        SyncInteractiveInputRegions();
        _logger?.Info("overlay", "Interactive SetWindowRgn sync requested (post-island).");
    }

    private bool TryCreateClientRect(FrameworkElement element, double scale, out OverlayInputRect rect)
    {
        rect = default;
        var width = element.ActualWidth > 0 ? element.ActualWidth : element.Width;
        var height = element.ActualHeight > 0 ? element.ActualHeight : element.Height;
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
        {
            return false;
        }

        GeneralTransform transform;
        try
        {
            transform = element.TransformToVisual(RootGrid);
        }
        catch (Exception)
        {
            return false;
        }

        var topLeft = transform.TransformPoint(new Point(0, 0));
        var x = (int)Math.Floor(topLeft.X * scale);
        var y = (int)Math.Floor(topLeft.Y * scale);
        var w = Math.Max(1, (int)Math.Ceiling(width * scale));
        var h = Math.Max(1, (int)Math.Ceiling(height * scale));
        rect = new OverlayInputRect(x, y, w, h);
        return true;
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

        if (instance.Type == WidgetTypes.Web)
        {
            var config = WebWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var view = new WebWidgetView();
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

        if (instance.Type == WidgetTypes.Calendar)
        {
            var config = CalendarWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var service = CalendarServiceFactory.Create(
                config,
                _timeProvider ?? new SystemTimeProvider(),
                secretStore: _secretStore,
                openBrowser: url => TryOpenBrowserUrl(url));
            var view = new CalendarWidgetView();
            view.Initialize(
                config,
                service,
                _timeProvider,
                openUrl: url => TryOpenHttpsUrl(url),
                onConfigurationChanged: updated =>
                {
                    instance.Configuration = updated.ToDictionary();
                    PersistLayoutNow();
                },
                cache: _calendarCache);
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        if (instance.Type == WidgetTypes.Music)
        {
            var config = MusicWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var view = new MusicWidgetView();
            view.Initialize(
                config,
                onConfigurationChanged: updated =>
                {
                    instance.Configuration = updated.ToDictionary();
                    PersistLayoutNow();
                },
                openUrl: url => TryOpenHttpsUrl(url));
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        if (instance.Type == WidgetTypes.Creative)
        {
            var config = CreativeWorkspaceWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var commands = _creativeCommands
                ?? new CreativeCommandService(
                    new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()));
            var view = new CreativeWorkspaceView();
            view.Initialize(
                commands,
                config,
                tryLaunch: TryLaunchCreativeItem,
                pickFile: PickCreativeFileAsync,
                pickFolder: PickCreativeFolderAsync,
                onConfigurationChanged: updated =>
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

    private string? TryLaunchCreativeItem(CreativeItem item)
    {
        if (_launcher is null)
        {
            return "Launch service unavailable.";
        }

        var exists = File.Exists(item.Path) || Directory.Exists(item.Path);
        if (!exists)
        {
            return "見つかりません — path was not found.";
        }

        var inferred = BlockTargetValidator.InferType(item.Path, Directory.Exists(item.Path));
        var result = _launcher.TryLaunch(new TargetLaunchRequest(
            Target: item.Path,
            ItemType: inferred.ToString(),
            DisplayName: item.Name));
        return result.Succeeded ? null : (result.ErrorMessage ?? "Launch failed.");
    }

    private async Task<(bool ok, bool cancelled, string? path, string? error)> PickCreativeFileAsync()
    {
        if (_pathPicker is null)
        {
            return (false, false, null, "Path picker unavailable.");
        }

        var result = await _pathPicker.PickFileAsync();
        return (result.Succeeded, result.Cancelled, result.Path, result.ErrorMessage);
    }

    private async Task<(bool ok, bool cancelled, string? path, string? error)> PickCreativeFolderAsync()
    {
        if (_pathPicker is null)
        {
            return (false, false, null, "Path picker unavailable.");
        }

        var result = await _pathPicker.PickFolderAsync();
        return (result.Succeeded, result.Cancelled, result.Path, result.ErrorMessage);
    }

    private bool TryOpenHttpsUrl(string url)
    {
        if (!WebUrlValidator.TryNormalize(url, out var normalized, out var error) || normalized is null)
        {
            _logger?.Warn("calendar", error ?? "Blocked calendar URL.");
            return false;
        }

        try
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri(normalized));
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Warn("calendar", $"Open Calendar failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Opens https OAuth consent URLs (no secrets in query logging).</summary>
    private bool TryOpenBrowserUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger?.Warn("calendar", "Blocked non-https OAuth browser URL.");
            return false;
        }

        try
        {
            _ = Windows.System.Launcher.LaunchUriAsync(uri);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.Warn("calendar", $"OAuth browser launch failed: {ex.Message}");
            return false;
        }
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

    private async void ThemeButton_Click(object sender, RoutedEventArgs e) =>
        await ShowThemeEditorDialogAsync();

    private void ArrangeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_layout is null)
        {
            return;
        }

        var areaWidth = Math.Max(WidgetCanvas.ActualWidth, ActualWidth);
        var areaHeight = Math.Max(WidgetCanvas.ActualHeight, ActualHeight);
        areaWidth = Math.Max(320, areaWidth);
        areaHeight = Math.Max(240, areaHeight);
        const double margin = 24;
        const double gap = 24;

        DesktopWidgetLayout.ArrangeEvenly(_layout.Widgets, areaWidth, areaHeight, margin, gap);

        var widgetBottom = _layout.Widgets.Count == 0
            ? margin
            : _layout.Widgets.Max(w => w.Position.Y + w.Size.Height) + gap;

        DesktopBlockLayout.ArrangeEvenlyBelow(
            _layout.Blocks,
            areaWidth,
            areaHeight,
            topOffset: widgetBottom,
            margin: margin,
            gap: gap);

        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("layout", "Arranged widgets and blocks evenly.");
        if (_debugChromeVisible)
        {
            StatusText.Text = "Arranged widgets & blocks evenly.";
        }
    }

    private async void ThemeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowThemeEditorDialogAsync();
    }

    private async void AddBlockButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddBlockDialogAsync();

    private async void AddBlockAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddBlockDialogAsync();
    }

    private async void AddWebButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddWebDialogAsync();

    private async void AddWebAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddWebDialogAsync();
    }

    private async void AddCalendarButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddCalendarDialogAsync();

    private async void AddCalendarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddCalendarDialogAsync();
    }

    private async void AddMusicButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddMusicDialogAsync();

    private async void AddMusicAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddMusicDialogAsync();
    }

    private async void AddCreativeButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddCreativeDialogAsync();

    private async void AddCreativeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddCreativeDialogAsync();
    }

    private async Task ShowAddCreativeDialogAsync()
    {
        if (_layout is null || _theme is null)
        {
            return;
        }

        AllowFullWindowInput();

        var xBox = new NumberBox
        {
            Header = "X",
            Value = 200,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var yBox = new NumberBox
        {
            Header = "Y",
            Value = 80,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var wBox = new NumberBox
        {
            Header = "Width",
            Value = 340,
            Minimum = Math.Max(_theme.WidgetMinWidth, 280),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var hBox = new NumberBox
        {
            Header = "Height",
            Value = 440,
            Minimum = Math.Max(_theme.WidgetMinHeight, 320),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var hint = new TextBlock
        {
            Text =
                "Desk for favorite projects and files. Register paths explicitly, then open with Windows defaults. "
                + "Not an Explorer. No delete/move. Commands: CreativeCommand → Service → safe open.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(hint);
        panel.Children.Add(xBox);
        panel.Children.Add(yBox);
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);

        var dialog = new ContentDialog
        {
            Title = "Add Creative Workspace",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var cascade = _layout.Widgets.Count(w => w.Type == WidgetTypes.Creative) * 24;
        var widget = DefaultWidgetFactory.CreateCreative(
            _layout.RoomId,
            xBox.Value + cascade,
            yBox.Value + cascade,
            wBox.Value,
            hBox.Value);

        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("creative", "Added Creative Workspace Widget.");
    }

    private async Task ShowAddMusicDialogAsync()
    {
        if (_layout is null || _theme is null)
        {
            return;
        }

        AllowFullWindowInput();

        var xBox = new NumberBox
        {
            Header = "X",
            Value = 120,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var yBox = new NumberBox
        {
            Header = "Y",
            Value = 120,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var wBox = new NumberBox
        {
            Header = "Width",
            Value = 360,
            Minimum = Math.Max(_theme.WidgetMinWidth, 280),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var hBox = new NumberBox
        {
            Header = "Height",
            Value = 420,
            Minimum = Math.Max(_theme.WidgetMinHeight, 280),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var hint = new TextBlock
        {
            Text =
                "Native Music UI: search Demo catalog, select a track, play/pause/next. "
                + "Spotify/YouTube APIs are not connected — optional browser open for registered web sources. "
                + "Commands go MusicCommand → Provider (future AI-ready). No Host Bridge.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(hint);
        panel.Children.Add(xBox);
        panel.Children.Add(yBox);
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);

        var dialog = new ContentDialog
        {
            Title = "Add Music Widget",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var cascade = _layout.Widgets.Count(w => w.Type == WidgetTypes.Music) * 24;
        var widget = DefaultWidgetFactory.CreateMusic(
            _layout.RoomId,
            xBox.Value + cascade,
            yBox.Value + cascade,
            wBox.Value,
            hBox.Value);

        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("music", "Added Music Widget (Spotify / YouTube / Local sources).");
    }

    private async Task ShowAddCalendarDialogAsync()
    {
        if (_layout is null || _theme is null)
        {
            return;
        }

        AllowFullWindowInput();

        var icsBox = new TextBox
        {
            Header = "Google Calendar secret ICS URL (optional)",
            PlaceholderText = "https://calendar.google.com/calendar/ical/…/basic.ics"
        };
        var xBox = new NumberBox
        {
            Header = "X",
            Value = 360,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var yBox = new NumberBox
        {
            Header = "Y",
            Value = 48,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var wBox = new NumberBox
        {
            Header = "Width",
            Value = 320,
            Minimum = Math.Max(_theme.WidgetMinWidth, 280),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var hBox = new NumberBox
        {
            Header = "Height",
            Value = 360,
            Minimum = Math.Max(_theme.WidgetMinHeight, 280),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var includeMock = new CheckBox
        {
            Content = "Include Mock provider (demo colors)",
            IsChecked = false
        };

        var hint = new TextBlock
        {
            Text =
                "Today's agenda via Calendar Hub (Local + optional Google). "
                + "Paste Google Calendar → Secret address in iCal format for ICS, "
                + "or place google-oauth-client.json under %LocalAppData%\\SecretBase\\credentials for API read. "
                + "Notion Calendar / TimeTree have no official sync API here — use Web Widget. "
                + "Tokens never go in layout JSON.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(hint);
        panel.Children.Add(icsBox);
        panel.Children.Add(includeMock);
        panel.Children.Add(xBox);
        panel.Children.Add(yBox);
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);

        var dialog = new ContentDialog
        {
            Title = "Add Calendar Widget",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var config = CalendarWidgetConfiguration.CreateDefault();
        config.IncludeMockProvider = includeMock.IsChecked == true;
        if (config.IncludeMockProvider)
        {
            config.UseSampleAgendaWhenEmpty = false;
        }

        var ics = icsBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(ics))
        {
            if (!WebUrlValidator.TryNormalize(ics, out var normalized, out var error) || normalized is null)
            {
                _logger?.Warn("calendar", error ?? "Invalid Google ICS URL.");
                if (_debugChromeVisible)
                {
                    StatusText.Text = error ?? "Invalid Google ICS URL.";
                }

                return;
            }

            config.GoogleIcsUrl = normalized;
            config.UseSampleAgendaWhenEmpty = false;
        }

        var cascade = _layout.Widgets.Count(w => w.Type == WidgetTypes.Calendar) * 24;
        var widget = DefaultWidgetFactory.CreateCalendar(
            _layout.RoomId,
            xBox.Value + cascade,
            yBox.Value + cascade,
            wBox.Value,
            hBox.Value,
            config);

        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("calendar",
            config.IncludeMockProvider
                ? "Added Calendar Widget (Mock provider)."
                : string.IsNullOrWhiteSpace(config.GoogleIcsUrl)
                    ? "Added Calendar Widget (local sample agenda)."
                    : "Added Calendar Widget (Google ICS provider).");
    }

    private async Task ShowAddWebDialogAsync()
    {
        if (_layout is null || _theme is null)
        {
            return;
        }

        AllowFullWindowInput();

        var urlBox = new TextBox
        {
            Header = "URL (https)",
            Text = WebWidgetConfiguration.DefaultUrl,
            PlaceholderText = "https://www.youtube.com/"
        };
        var xBox = new NumberBox
        {
            Header = "X",
            Value = 96,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var yBox = new NumberBox
        {
            Header = "Y",
            Value = 96,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var wBox = new NumberBox
        {
            Header = "Width",
            Value = 560,
            Minimum = Math.Max(_theme.WidgetMinWidth, 320),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var hBox = new NumberBox
        {
            Header = "Height",
            Value = 360,
            Minimum = Math.Max(_theme.WidgetMinHeight, 220),
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var hint = new TextBlock
        {
            Text = "Web pages are Untrusted. No host bridge to Secret Base.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(urlBox);
        panel.Children.Add(hint);
        panel.Children.Add(xBox);
        panel.Children.Add(yBox);
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);

        var dialog = new ContentDialog
        {
            Title = "Add Web Widget",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (!WebUrlValidator.TryNormalize(urlBox.Text, out var normalized, out var error))
        {
            _logger?.Warn("widget", error ?? WebUrlValidator.BlockedMessage);
            if (_debugChromeVisible)
            {
                StatusText.Text = error ?? WebUrlValidator.BlockedMessage;
            }

            return;
        }

        var cascade = _layout.Widgets.Count(w => w.Type == WidgetTypes.Web) * 24;
        var widget = DefaultWidgetFactory.CreateWeb(
            normalized,
            _layout.RoomId,
            xBox.Value + cascade,
            yBox.Value + cascade,
            wBox.Value,
            hBox.Value);

        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("widget", $"Added Web Widget → {normalized}");
    }

    private async Task ShowAddBlockDialogAsync()
    {
        if (_layout is null || _theme is null || _launcher is null || _icons is null || _intake is null)
        {
            return;
        }

        // ContentDialog lives in the same HWND; expand hit region so the dialog is clickable.
        AllowFullWindowInput();

        var nameBox = new TextBox
        {
            Header = "Name",
            Text = "DEVELOPMENT",
            PlaceholderText = "Block name"
        };
        var xBox = new NumberBox
        {
            Header = "X",
            Value = 420,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var yBox = new NumberBox
        {
            Header = "Y",
            Value = 48,
            Minimum = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var wBox = new NumberBox
        {
            Header = "Width",
            Value = Block.DefaultWidth,
            Minimum = Block.MinWidth,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var hBox = new NumberBox
        {
            Header = "Height",
            Value = Block.DefaultHeight,
            Minimum = Block.MinHeight,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(nameBox);
        panel.Children.Add(xBox);
        panel.Children.Add(yBox);
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);

        var dialog = new ContentDialog
        {
            Title = "Add Block",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var block = DefaultBlockFactory.Create(
            nameBox.Text,
            _layout.RoomId,
            xBox.Value,
            yBox.Value,
            wBox.Value,
            hBox.Value);

        // Cascade slightly when creating multiple near the same spot.
        var offset = _layout.Blocks.Count * 24;
        block.Position.X = Math.Max(0, block.Position.X + offset);
        block.Position.Y = Math.Max(0, block.Position.Y + offset);

        _layout.Blocks.Add(block);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("block", $"Created Block '{block.Name}' ({block.Id}).");
    }

    private async Task ShowThemeEditorDialogAsync()
    {
        if (_theme is null || _themeStore is null)
        {
            return;
        }

        AllowFullWindowInput();

        var draft = ThemeDefinition.CreateDefault();
        ThemePresets.CopyVisualsTo(_theme, draft);
        draft.Id = _theme.Id;
        draft.WidgetMinWidth = _theme.WidgetMinWidth;
        draft.WidgetMinHeight = _theme.WidgetMinHeight;

        var presetBox = new ComboBox
        {
            Header = "Preset",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SelectedItem = ThemePresets.Names.Contains(draft.DisplayName) ? draft.DisplayName : "Default"
        };
        foreach (var name in ThemePresets.Names)
        {
            presetBox.Items.Add(name);
        }

        var widgetBg = CreateColorBox("Widget / Block background (#AARRGGBB)", draft.WidgetBackground);
        var widgetFg = CreateColorBox("Clock / Text / title color", draft.WidgetForeground);
        var mutedFg = CreateColorBox("Date / muted text", draft.ForegroundMuted);
        var accent = CreateColorBox("Accent (buttons)", draft.Accent);
        var fontBox = new ComboBox
        {
            Header = "Font",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var font in new[]
                 {
                     "Segoe UI Variable Display",
                     "Segoe UI",
                     "Georgia",
                     "Cascadia Mono",
                     "Consolas",
                     "Yu Gothic UI"
                 })
        {
            fontBox.Items.Add(font);
        }

        fontBox.SelectedItem = fontBox.Items.Contains(draft.FontFamily) ? draft.FontFamily : fontBox.Items[0];

        var radiusBox = new NumberBox
        {
            Header = "Corner radius",
            Value = draft.CornerRadius,
            Minimum = 0,
            Maximum = 40,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };
        var opacityBox = new NumberBox
        {
            Header = "Surface opacity (0.35–1.0)",
            Value = draft.Transparency,
            Minimum = 0.35,
            Maximum = 1.0,
            SmallChange = 0.05,
            LargeChange = 0.1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var preview = new Border
        {
            Height = 72,
            CornerRadius = new CornerRadius(draft.CornerRadius),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 0)
        };
        var previewTime = new TextBlock
        {
            Text = "14:35:08",
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        var previewDate = new TextBlock { Text = "2026 / 08 / 13", FontSize = 12, Opacity = 0.9 };
        var previewStack = new StackPanel { Spacing = 2 };
        previewStack.Children.Add(previewTime);
        previewStack.Children.Add(previewDate);
        preview.Child = previewStack;

        void RefreshPreview()
        {
            preview.Background = ThemePainter.Brush(widgetBg.Text, Math.Clamp(opacityBox.Value, 0.35, 1.0));
            preview.CornerRadius = new CornerRadius(Math.Clamp(radiusBox.Value, 0, 40));
            previewTime.Foreground = ThemePainter.Brush(widgetFg.Text);
            previewDate.Foreground = ThemePainter.Brush(mutedFg.Text);
            var font = fontBox.SelectedItem as string ?? draft.FontFamily;
            previewTime.FontFamily = new FontFamily(font);
            previewDate.FontFamily = new FontFamily(font);
        }

        presetBox.SelectionChanged += (_, _) =>
        {
            if (presetBox.SelectedItem is not string preset)
            {
                return;
            }

            ThemePresets.ApplyPreset(draft, preset);
            widgetBg.Text = draft.WidgetBackground;
            widgetFg.Text = draft.WidgetForeground;
            mutedFg.Text = draft.ForegroundMuted;
            accent.Text = draft.Accent;
            radiusBox.Value = draft.CornerRadius;
            opacityBox.Value = draft.Transparency;
            if (fontBox.Items.Contains(draft.FontFamily))
            {
                fontBox.SelectedItem = draft.FontFamily;
            }

            RefreshPreview();
        };

        widgetBg.TextChanged += (_, _) => RefreshPreview();
        widgetFg.TextChanged += (_, _) => RefreshPreview();
        mutedFg.TextChanged += (_, _) => RefreshPreview();
        radiusBox.ValueChanged += (_, _) => RefreshPreview();
        opacityBox.ValueChanged += (_, _) => RefreshPreview();
        fontBox.SelectionChanged += (_, _) => RefreshPreview();
        RefreshPreview();

        var panel = new StackPanel { Spacing = 8, Width = 360 };
        panel.Children.Add(new TextBlock
        {
            Text = "Applies to Clock, date, Text boxes, and Blocks.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            FontSize = 12
        });
        panel.Children.Add(presetBox);
        panel.Children.Add(preview);
        panel.Children.Add(widgetBg);
        panel.Children.Add(widgetFg);
        panel.Children.Add(mutedFg);
        panel.Children.Add(accent);
        panel.Children.Add(fontBox);
        panel.Children.Add(radiusBox);
        panel.Children.Add(opacityBox);

        var dialog = new ContentDialog
        {
            Title = "Theme",
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            },
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            SyncInteractiveInputRegions();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (!TryNormalizeHex(widgetBg.Text, out var bg) ||
            !TryNormalizeHex(widgetFg.Text, out var fg) ||
            !TryNormalizeHex(mutedFg.Text, out var muted) ||
            !TryNormalizeHex(accent.Text, out var ac))
        {
            _logger?.Warn("theme", "Theme apply cancelled — invalid color hex.");
            if (_debugChromeVisible)
            {
                StatusText.Text = "Invalid color. Use #RRGGBB or #AARRGGBB.";
            }

            return;
        }

        draft.WidgetBackground = bg;
        draft.WidgetForeground = fg;
        draft.ForegroundMuted = muted;
        draft.Accent = ac;
        draft.CornerRadius = Math.Clamp(radiusBox.Value, 0, 40);
        draft.Transparency = Math.Clamp(opacityBox.Value, 0.35, 1.0);
        draft.FontFamily = fontBox.SelectedItem as string ?? draft.FontFamily;
        if (presetBox.SelectedItem is string selectedPreset)
        {
            draft.DisplayName = selectedPreset;
        }

        ThemePresets.CopyVisualsTo(draft, _theme);
        _theme.Id = draft.Id;
        _theme.WidgetMinWidth = draft.WidgetMinWidth;
        _theme.WidgetMinHeight = draft.WidgetMinHeight;

        try
        {
            _themeStore.Save(_theme);
            ApplyDesktopTheme(_theme);
            StyleFabButtons(_theme);
            RenderDesktopObjects();
            RefreshDebugStatus();
            _logger?.Info("theme", $"Theme applied ({_theme.DisplayName}).");
        }
        catch (Exception ex)
        {
            _logger?.Error("theme", "Failed to save theme.", ex);
        }
    }

    private static TextBox CreateColorBox(string header, string value) =>
        new()
        {
            Header = header,
            Text = value,
            PlaceholderText = "#AARRGGBB"
        };

    private static bool TryNormalizeHex(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var value = input.Trim();
        if (!value.StartsWith('#'))
        {
            value = "#" + value;
        }

        var hex = value[1..];
        if (hex.Length is not (6 or 8))
        {
            return false;
        }

        foreach (var c in hex)
        {
            var ok = c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
            if (!ok)
            {
                return false;
            }
        }

        normalized = "#" + hex.ToUpperInvariant();
        return true;
    }

    private void AllowFullWindowInput()
    {
        if (_overlay is null || _overlayTarget is null || XamlRoot is null)
        {
            return;
        }

        var scale = XamlRoot.RasterizationScale;
        var width = Math.Max(1, (int)Math.Ceiling(ActualWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(ActualHeight * scale));
        _overlay.UpdateInteractiveInputRegions(
            _overlayTarget,
            [new OverlayInputRect(0, 0, width, height)]);
    }

    private void DeleteBlock(Block block)
    {
        if (_layout is null)
        {
            return;
        }

        _layout.Blocks.RemoveAll(b => b.Id == block.Id);
        PersistLayoutNow();
        RenderDesktopObjects();
        RefreshDebugStatus();
        _logger?.Info("block", $"Deleted Block '{block.Name}' ({block.Id}).");
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
            _logger?.Info("persistence", $"Layout saved ({_layout.Widgets.Count} widgets, {_layout.Blocks.Count} blocks).");
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
