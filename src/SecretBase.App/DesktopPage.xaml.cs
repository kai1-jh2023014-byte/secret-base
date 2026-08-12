using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.App.Desktop;
using SecretBase.Core;
using SecretBase.Core.Blocks;
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

        _theme = _themeStore.LoadOrCreateDefault();
        _layout = _layoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);
        EnsureSeedTextWidget(_layout);

        ApplyDesktopTheme(_theme);
        StyleAddBlockFab(_theme);
        RefreshDebugStatus();
        ShowDebugChrome(forceVisible: false);

        RenderDesktopObjects();
        _logger.Info("desktop", $"Overlay desktop shown for room '{_layout.RoomId}' with {_layout.Widgets.Count} widget(s), {_layout.Blocks.Count} block(s).");
        _logger.Info("widget", "Clock and Text widget hosts ready.");
        _logger.Info("block", "Block host ready (use + button to add; drop + drag icons inside a Block).");
    }

    private void StyleAddBlockFab(ThemeDefinition theme)
    {
        AddBlockFab.Background = ThemePainter.Brush(theme.Accent, 0.92);
        AddBlockFab.Foreground = ThemePainter.Brush(theme.Foreground);
        AddBlockFab.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.35);
        AddBlockFab.BorderThickness = new Thickness(1);
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

        _overlay.UpdateInteractiveInputRegions(_overlayTarget, rects);
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

    private async void AddBlockButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddBlockDialogAsync();

    private async void AddBlockAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddBlockDialogAsync();
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
