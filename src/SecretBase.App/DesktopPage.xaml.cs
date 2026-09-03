using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.App.Desktop;
using SecretBase.Core;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
using SecretBase.Core.Assistant;
using SecretBase.Core.Blocks;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Desktop;
using SecretBase.Core.Integration;
using SecretBase.Core.Music;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Core.Widgets.Apps;
using SecretBase.Core.Widgets.Assistant;
using SecretBase.Core.Widgets.Calendar;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Core.Widgets.Creative;
using SecretBase.Core.Widgets.Music;
using SecretBase.Core.Widgets.Text;
using SecretBase.Core.Widgets.Web;
using SecretBase.Infrastructure.Assistant;
using SecretBase.Infrastructure.Music;
using SecretBase.Infrastructure.Calendar;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Windows;
using SecretBase.Widgets.Ai;
using SecretBase.Widgets.Apps;
using SecretBase.Widgets.Assistant;
using SecretBase.Widgets.Calendar;
using SecretBase.Widgets.Clock;
using SecretBase.Widgets.Creative;
using SecretBase.Widgets.Music;
using SecretBase.Widgets.Text;
using SecretBase.Widgets.Theming;
using SecretBase.Widgets.Web;
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
    private ICursorLaunchService? _cursorLaunch;
    private AiCommandService? _aiCommands;
    private AppCommandService? _appCommands;
    private CalendarCommandService? _calendarCommands;
    private MusicCommandService? _musicCommands;
    private IntegrationCommandService? _integrationCommands;
    private IAssistantService? _assistant;
    private IAssistantSettingsStore? _assistantSettings;
    private IAutoStartService? _autoStart;
    private IAppLaunchSettingsStore? _launchSettingsStore;
    private bool _autoStartToggleSync;
    private IAiProviderFactory? _assistantProviders;
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
        _cursorLaunch = args.CursorLaunch ?? new WindowsCursorLaunchService();
        var projectService = args.CreativeCommands?.Projects
            ?? new CreativeProjectService(new JsonCreativeProjectStore());
        _aiCommands = args.AiCommands
            ?? new AiCommandService(
                AiWorkspaceWidgetConfiguration.CreateDefault(),
                projectService,
                () => _cursorLaunch.IsAvailable);
        _creativeCommands = args.CreativeCommands
            ?? new CreativeCommandService(
                new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()),
                projectService,
                _aiCommands);
        _appCommands = args.AppCommands
            ?? new AppCommandService(new CustomAppService(new JsonCustomAppStore()), projectService);
        var time = _timeProvider ?? new SystemTimeProvider();
        _calendarCommands = new CalendarCommandService(
            CalendarServiceFactory.Create(
                CalendarWidgetConfiguration.CreateDefault(),
                time,
                secretStore: _secretStore,
                openBrowser: url => TryOpenHttpsUrl(url)),
            time);
        _musicCommands = new MusicCommandService(MusicServiceFactory.Create(_secretStore, TryOpenHttpsUrl));
        _integrationCommands = new IntegrationCommandService(
            calendar: _calendarCommands,
            music: _musicCommands,
            creative: _creativeCommands,
            apps: _appCommands,
            ai: _aiCommands);
        _assistantSettings = new JsonAssistantSettingsStore();
        _autoStart = args.AutoStart ?? new WindowsRegistryAutoStartService();
        _launchSettingsStore = args.LaunchSettingsStore ?? new JsonAppLaunchSettingsStore();
        _assistantProviders = new AssistantProviderFactory(_secretStore);
        var assistantRegistry = BuiltinAssistantToolRegistry.Instance;
        var assistantContext = new AssistantContextService(
            calendar: _calendarCommands,
            creative: _creativeCommands,
            apps: _appCommands,
            music: _musicCommands,
            settings: () => _assistantSettings.LoadOrCreate(),
            isOpenAiKeyConfigured: () =>
                _secretStore is not null
                && _secretStore.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var key)
                && !string.IsNullOrWhiteSpace(key),
            isGeminiKeyConfigured: () =>
                _secretStore is not null
                && _secretStore.TryGetSecret(AssistantSecretKeys.GeminiApiKey, out var gemini)
                && !string.IsNullOrWhiteSpace(gemini));
        _assistant = new AssistantService(
            assistantRegistry,
            new AssistantToolExecutor(
                assistantRegistry,
                _calendarCommands,
                _creativeCommands,
                _aiCommands,
                _integrationCommands,
                _appCommands,
                _musicCommands,
                assistantContext),
            () => _assistantProviders.Create(_assistantSettings.LoadOrCreate()),
            () => _assistantSettings.LoadOrCreate(),
            assistantContext);

        _theme = _themeStore.LoadOrCreateDefault();
        _layout = _layoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);
        EnsureSeedTextWidget(_layout);

        ApplyDesktopTheme(_theme);
        StyleFabButtons(_theme);
        RefreshDebugStatus();
        InitializeAutoStartToggle();
        ShowDebugChrome(forceVisible: false);

        RenderDesktopObjects();
        InitializeTaskbarAiChat();
        _logger.Info("desktop", $"Overlay desktop shown for room '{_layout.RoomId}' with {_layout.Widgets.Count} widget(s), {_layout.Blocks.Count} block(s).");
        _logger.Info("widget", "Widget hosts ready (Clock, Text, Web, Calendar, Music, Creative, AI Workspace, Apps, Secret Base AI).");
        _logger.Info("assistant", "Taskbar AI chat bar ready (Ctrl+Shift+K). Does not replace Windows Search.");
        _logger.Info("block", "Block host ready (use Blk button to add; drop + drag icons inside a Block).");
        _logger.Info("theme", "Theme editor ready (Aa button) — colors apply to all widgets and Blocks.");
        _logger.Info("layout", "Arrange ready (Grid button) — even placement for widgets and blocks.");
        _logger.Info("widget", "Add Widget (+) — grouped catalog (Information / Creative / AI / Apps). Classroom opens the existing Web Widget.");
    }

    private void StyleFabButtons(ThemeDefinition theme)
    {
        void StylePrimary(Button button)
        {
            button.Background = ThemePainter.Brush(theme.Accent, 0.9);
            button.Foreground = ThemePainter.Brush(theme.Foreground);
            button.BorderBrush = ThemePainter.Brush(theme.Border, 0.35);
            button.BorderThickness = new Thickness(1);
            button.CornerRadius = new CornerRadius(Math.Max(12, theme.CornerRadius * 0.7));
            button.FontFamily = new FontFamily(theme.FontFamily);
        }

        void StyleSecondary(Button button)
        {
            button.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
            button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
            button.BorderBrush = ThemePainter.Brush(theme.Border, 0.5);
            button.BorderThickness = new Thickness(1);
            button.CornerRadius = new CornerRadius(Math.Max(12, theme.CornerRadius * 0.7));
            button.FontFamily = new FontFamily(theme.FontFamily);
        }

        StylePrimary(AddWidgetFab);
        StyleSecondary(AddBlockFab);
        StyleSecondary(ThemeFab);
        StyleSecondary(ArrangeFab);
        TaskbarAiChat.ApplyTheme(theme);

        if (HostStatusLabel is not null)
        {
            HostStatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
            HostStatusLabel.FontFamily = new FontFamily(theme.FontFamily);
            HostStatusLabel.CharacterSpacing = 20;
        }
    }

    private void ApplyDesktopTheme(ThemeDefinition theme)
    {
        RootGrid.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        WidgetCanvas.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        StatusText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusText.FontFamily = new FontFamily(theme.FontFamily);
        DebugChrome.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        DebugChrome.CornerRadius = new CornerRadius(Math.Max(8, theme.CornerRadius / 2));

        if (HostStatusLabel is not null)
        {
            HostStatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
            HostStatusLabel.FontFamily = new FontFamily(theme.FontFamily);
        }

        TaskbarAiChat.ApplyTheme(theme);
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

    private void InitializeTaskbarAiChat()
    {
        var enabled = _launchSettingsStore?.LoadOrCreate().TaskbarAiChatEnabled ?? true;
        if (!enabled || _assistant is null)
        {
            TaskbarAiChat.Visibility = Visibility.Collapsed;
            return;
        }

        TaskbarAiChat.Initialize(_assistant, TryApplyAssistantLaunch);
        TaskbarAiChat.LayoutChanged += SyncInteractiveInputRegions;
        if (_theme is not null)
        {
            TaskbarAiChat.ApplyTheme(_theme);
        }

        TaskbarAiChat.Loaded += (_, _) => SyncInteractiveInputRegions();
    }

    private void InitializeAutoStartToggle()
    {
        if (_launchSettingsStore is null || _autoStart is null)
        {
            AutoStartToggle.Visibility = Visibility.Collapsed;
            return;
        }

        if (!_autoStart.IsSupported)
        {
            AutoStartToggle.Visibility = Visibility.Collapsed;
            return;
        }

        _autoStartToggleSync = true;
        var settings = _launchSettingsStore.LoadOrCreate();
        var status = _autoStart.GetStatus();
        AutoStartToggle.IsOn = settings.LaunchAtWindowsLogin && status.PointsToCurrentExecutable;
        AutoStartToggle.IsEnabled = _autoStart.TryGetStartupExecutablePath(out _, out _) || status.IsRegistered;
        _autoStartToggleSync = false;
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_autoStartToggleSync || _autoStart is null || _launchSettingsStore is null)
        {
            return;
        }

        var desired = AutoStartToggle.IsOn;
        _autoStartToggleSync = true;
        if (!AutoStartCoordinator.TrySetEnabled(
                desired,
                _autoStart,
                _launchSettingsStore,
                _logger,
                out var error))
        {
            AutoStartToggle.IsOn = !desired;
            _logger?.Warn("startup", error ?? "Auto-start could not be updated.");
            HostStatusLabel.Text = error ?? "Auto-start could not be updated.";
            HostStatusLabel.Visibility = Visibility.Visible;
        }
        else
        {
            HostStatusLabel.Visibility = Visibility.Collapsed;
            _logger?.Info("startup", desired ? "User enabled Windows logon auto-start." : "User disabled Windows logon auto-start.");
        }

        _autoStartToggleSync = false;
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
                onBoundsChanged: SyncInteractiveInputRegions,
                onRemoveRequested: RemoveWidget);
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
                onStatus: ShowHostStatus);
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

        if (TryCreateClientRect(AddWidgetFab, scale, out var addWidgetFabRect))
        {
            rects.Add(addWidgetFabRect);
        }

        if (TryCreateClientRect(AddBlockFab, scale, out var fabRect))
        {
            rects.Add(fabRect);
        }

        if (TryCreateClientRect(ThemeFab, scale, out var themeFabRect))
        {
            rects.Add(themeFabRect);
        }

        if (TryCreateClientRect(ArrangeFab, scale, out var arrangeFabRect))
        {
            rects.Add(arrangeFabRect);
        }

        if (TaskbarAiChat.Visibility == Visibility.Visible
            && TryCreateClientRect(TaskbarAiChat, scale, out var chatRect))
        {
            rects.Add(chatRect);
        }

        if (HostStatusLabel.Visibility == Visibility.Visible
            && TryCreateClientRect(HostStatusLabel, scale, out var statusRect))
        {
            rects.Add(statusRect);
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
            instance.Configuration = config.ToDictionary();

            var view = new ClockWidgetView();
            view.Initialize(
                config,
                _timeProvider,
                onConfigurationChanged: updated =>
                {
                    instance.Configuration = updated.ToDictionary();
                    PersistLayoutNow();
                });
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

            _widgetDisposables.Add(view);
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
                openUrl: url => TryOpenHttpsUrl(url),
                musicService: _musicCommands.MusicService);
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
                    new CreativeWorkspaceService(new JsonCreativeWorkspaceStore()),
                    new CreativeProjectService(new JsonCreativeProjectStore()),
                    _aiCommands);
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
                },
                tryLaunchTarget: TryLaunchCreativeTarget,
                tryLaunchCursor: TryLaunchCursor);
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        if (instance.Type == WidgetTypes.Ai)
        {
            var config = AiWorkspaceWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            if (_assistant is null || _assistantSettings is null || _secretStore is null || _assistantProviders is null)
            {
                return null;
            }

            var view = new AiWorkspaceView();
            view.Initialize(
                _assistant,
                _assistantSettings,
                _secretStore,
                _assistantProviders,
                TryApplyAssistantLaunch);
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        if (instance.Type == WidgetTypes.Apps)
        {
            var config = AppsWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();

            var apps = _appCommands
                ?? new AppCommandService(
                    new CustomAppService(new JsonCustomAppStore()),
                    _creativeCommands?.Projects);
            var view = new AppsWidgetView();
            view.Initialize(
                apps,
                tryExecute: TryExecuteAppResult,
                pickFile: PickCreativeFileAsync,
                pickFolder: PickCreativeFolderAsync);
            if (_theme is not null)
            {
                view.ApplyTheme(_theme);
            }

            return view;
        }

        if (instance.Type == WidgetTypes.Assistant)
        {
            var config = AssistantWidgetConfiguration.FromDictionary(instance.Configuration);
            instance.Configuration = config.ToDictionary();
            if (_assistant is null || _assistantSettings is null || _secretStore is null || _assistantProviders is null)
            {
                return null;
            }

            var view = new AssistantWidgetView();
            view.Initialize(
                _assistant,
                _assistantSettings,
                _secretStore,
                _assistantProviders,
                TryApplyAssistantLaunch);
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
        return TryLaunchCreativePath(item.Path, item.Name);
    }

    /// <summary>
    /// Opens a validated project path or https link. Never deletes/moves/renames.
    /// </summary>
    private string? TryLaunchCreativeTarget(string target, bool isExternalLink)
    {
        if (isExternalLink)
        {
            return TryOpenHttpsUrl(target)
                ? null
                : "Could not open link in the system browser.";
        }

        return TryLaunchCreativePath(target, displayName: null);
    }

    private string? TryLaunchCreativePath(string path, string? displayName)
    {
        if (_launcher is null)
        {
            return "Launch service unavailable.";
        }

        var exists = File.Exists(path) || Directory.Exists(path);
        if (!exists)
        {
            return "見つかりません — path was not found.";
        }

        var inferred = BlockTargetValidator.InferType(path, Directory.Exists(path));
        var result = _launcher.TryLaunch(new TargetLaunchRequest(
            Target: path,
            ItemType: inferred.ToString(),
            DisplayName: displayName ?? Path.GetFileName(path.TrimEnd('\\', '/'))));
        return result.Succeeded ? null : (result.ErrorMessage ?? "Launch failed.");
    }

    private string? TryLaunchCursor(string? folderPath, bool openAppOnly)
    {
        var cursor = _cursorLaunch ?? new WindowsCursorLaunchService();
        if (openAppOnly || string.IsNullOrWhiteSpace(folderPath))
        {
            var app = cursor.TryOpenApp();
            return app.Succeeded ? null : (app.ErrorMessage ?? "Cursor is not available.");
        }

        var result = cursor.TryOpenFolder(folderPath);
        return result.Succeeded ? null : (result.ErrorMessage ?? "Could not open folder in Cursor.");
    }

    private string? TryExecuteAiResult(AiCommandResult result)
    {
        if (!result.Succeeded)
        {
            return result.ErrorMessage ?? "AI open failed.";
        }

        if (result.ShouldOpenCursorAtFolder && !string.IsNullOrWhiteSpace(result.FolderPath))
        {
            return TryLaunchCursor(result.FolderPath, openAppOnly: false);
        }

        if (result.ShouldOpenCursorApp)
        {
            return TryLaunchCursor(null, openAppOnly: true);
        }

        if (result.ShouldOpenUrl && !string.IsNullOrWhiteSpace(result.Url))
        {
            return TryOpenHttpsUrl(result.Url!)
                ? null
                : "Could not open AI website in the system browser.";
        }

        return "Nothing to open.";
    }

    private string? TryExecuteAppResult(AppCommandResult result)
    {
        if (!result.Succeeded)
        {
            return result.ErrorMessage ?? "App command failed.";
        }

        if (result.ShouldOpenCursorAtFolder && !string.IsNullOrWhiteSpace(result.CursorFolderPath))
        {
            return TryLaunchCursor(result.CursorFolderPath, false);
        }

        if (!result.ShouldLaunch || string.IsNullOrWhiteSpace(result.LaunchTarget))
        {
            return null;
        }

        if (result.LaunchIsExternalLink)
        {
            return TryOpenHttpsUrl(result.LaunchTarget)
                ? null
                : "Could not open app URL in the system browser.";
        }

        return TryLaunchCreativePath(result.LaunchTarget, result.App?.Name);
    }

    private string? TryApplyAssistantLaunch(AssistantTurnResult result)
    {
        if (result.ShouldOpenCursorAtFolder && !string.IsNullOrWhiteSpace(result.CursorFolderPath))
        {
            var cursorError = TryLaunchCursor(result.CursorFolderPath, false);
            return cursorError is null ? null : AssistantUserMessages.CursorOpenFailed;
        }

        if (!result.ShouldLaunch || string.IsNullOrWhiteSpace(result.LaunchTarget))
        {
            return null;
        }

        if (result.LaunchIsExternalLink)
        {
            return TryOpenHttpsUrl(result.LaunchTarget)
                ? null
                : "Could not open link in the system browser.";
        }

        return TryLaunchCreativePath(result.LaunchTarget, null);
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
            _logger?.Info("widget", "Seeded default Text widget.");
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

    private async void AddWidgetButton_Click(object sender, RoutedEventArgs e) =>
        await ShowAddWidgetCatalogAsync();

    private async void AddWidgetAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ShowAddWidgetCatalogAsync();
    }

    private async void AddWebAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddWidgetByTypeAsync(WidgetTypes.Web);
    }

    private async void AddCalendarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddWidgetByTypeAsync(WidgetTypes.Calendar);
    }

    private async void AddMusicAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddWidgetByTypeAsync(WidgetTypes.Music);
    }

    private async void AddCreativeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddWidgetByTypeAsync(WidgetTypes.Creative);
    }

    private async void AddAiAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await AddWidgetByTypeAsync(WidgetTypes.Ai);
    }

    private void TaskbarAiChatAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (TaskbarAiChat.Visibility != Visibility.Visible)
        {
            return;
        }

        TaskbarAiChat.FocusInput();
    }

    private async Task ShowAddWidgetCatalogAsync()
    {
        if (_layout is null)
        {
            return;
        }

        AllowFullWindowInput();

        var list = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            Height = 320
        };
        var firstSelectable = -1;
        foreach (var group in WidgetCatalog.Entries.GroupBy(entry => entry.Group))
        {
            list.Items.Add(new ListViewItem
            {
                Content = group.Key,
                IsEnabled = false,
                Tag = null
            });
            foreach (var catalogEntry in group)
            {
                if (firstSelectable < 0)
                {
                    firstSelectable = list.Items.Count;
                }

                list.Items.Add(new ListViewItem { Content = "  " + catalogEntry.Label, Tag = catalogEntry.Id });
            }
        }

        if (firstSelectable >= 0)
        {
            list.SelectedIndex = firstSelectable;
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Choose a widget. Classroom opens the existing Web Widget at Google Classroom.",
            FontSize = 12,
            Opacity = 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(list);

        var dialog = new ContentDialog
        {
            Title = "Add Widget",
            PrimaryButtonText = "Add",
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

        if (list.SelectedItem is ListViewItem item && item.Tag is string catalogId)
        {
            await AddWidgetFromCatalogAsync(catalogId);
        }
    }

    private Task AddWidgetFromCatalogAsync(string catalogId)
    {
        var entry = WidgetCatalog.FindById(catalogId);
        if (entry is null)
        {
            ShowHostStatus($"Unknown catalog item: {catalogId}");
            return Task.CompletedTask;
        }

        if (entry.Kind == WidgetCatalogKind.WebPreset)
        {
            if (!WidgetCatalog.TryResolveWebPresetUrl(entry, out var url, out var error))
            {
                ShowHostStatus(error);
                return Task.CompletedTask;
            }

            return AddWebPresetAsync(url, entry.Label);
        }

        if (string.IsNullOrWhiteSpace(entry.WidgetType))
        {
            ShowHostStatus($"Unknown widget type for {entry.Label}.");
            return Task.CompletedTask;
        }

        return AddWidgetByTypeAsync(entry.WidgetType);
    }

    private Task AddWebPresetAsync(string url, string label)
    {
        if (_layout is null)
        {
            return Task.CompletedTask;
        }

        var cascade = _layout.Widgets.Count(w =>
            string.Equals(w.Type, WidgetTypes.Web, StringComparison.OrdinalIgnoreCase)) * 24;
        var widget = DefaultWidgetFactory.CreateWeb(
            url: url,
            roomId: _layout.RoomId,
            x: 96 + cascade,
            y: 96 + cascade);
        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        ShowHostStatus($"Added {label}.");
        SyncInteractiveInputRegions();
        RefreshDebugStatus();
        _logger?.Info("widget", $"Added web preset {label} ({widget.Id}).");
        return Task.CompletedTask;
    }

    private Task AddWidgetByTypeAsync(string type)
    {
        if (_layout is null)
        {
            return Task.CompletedTask;
        }

        var cascade = _layout.Widgets.Count(w =>
            string.Equals(w.Type, type, StringComparison.OrdinalIgnoreCase)) * 24;

        WidgetInstance? widget = type switch
        {
            WidgetTypes.Clock => DefaultWidgetFactory.CreateClock(
                _layout.RoomId, 48 + cascade, 48 + cascade),
            WidgetTypes.Text => DefaultWidgetFactory.CreateText(
                _layout.RoomId, 48 + cascade, 240 + cascade),
            WidgetTypes.Web => DefaultWidgetFactory.CreateWeb(
                roomId: _layout.RoomId, x: 96 + cascade, y: 96 + cascade),
            WidgetTypes.Calendar => DefaultWidgetFactory.CreateCalendar(
                _layout.RoomId, 360 + cascade, 48 + cascade),
            WidgetTypes.Music => DefaultWidgetFactory.CreateMusic(
                _layout.RoomId, 120 + cascade, 120 + cascade),
            WidgetTypes.Ai => DefaultWidgetFactory.CreateAi(
                _layout.RoomId, 240 + cascade, 100 + cascade),
            WidgetTypes.Creative => DefaultWidgetFactory.CreateCreative(
                _layout.RoomId, 200 + cascade, 80 + cascade),
            WidgetTypes.Apps => DefaultWidgetFactory.CreateApps(
                _layout.RoomId, 280 + cascade, 80 + cascade),
            WidgetTypes.Assistant => DefaultWidgetFactory.CreateAssistant(
                _layout.RoomId, 320 + cascade, 60 + cascade),
            _ => null
        };

        if (widget is null)
        {
            ShowHostStatus($"Unknown widget type: {type}");
            return Task.CompletedTask;
        }

        _layout.Widgets.Add(widget);
        PersistLayoutNow();
        RenderDesktopObjects();
        ShowHostStatus($"Added {type} widget.");
        SyncInteractiveInputRegions();
        RefreshDebugStatus();
        _logger?.Info("widget", $"Added widget {type} ({widget.Id}).");
        return Task.CompletedTask;
    }

    private void RemoveWidget(WidgetInstance instance)
    {
        if (_layout is null)
        {
            return;
        }

        _layout.Widgets.RemoveAll(w => w.Id == instance.Id);
        PersistLayoutNow();
        RenderDesktopObjects();
        ShowHostStatus($"Removed widget ({instance.Type}).");
        _logger?.Info("widget", $"Removed widget {instance.Type} ({instance.Id}).");
    }

    private void ShowHostStatus(string message)
    {
        _logger?.Info("desktop", message);
        if (HostStatusLabel is not null)
        {
            HostStatusLabel.Text = message;
            HostStatusLabel.Visibility = Visibility.Visible;
        }

        if (_debugChromeVisible)
        {
            StatusText.Text = message;
        }

        SyncInteractiveInputRegions();
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
            SelectedItem = ThemePresets.Names.Contains(draft.DisplayName) ? draft.DisplayName : "Atelier"
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

        var previewOuter = new Border
        {
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(draft.CornerRadius + 2),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 4, 0, 0)
        };
        var preview = new Border
        {
            MinHeight = 88,
            CornerRadius = new CornerRadius(draft.CornerRadius),
            Padding = new Thickness(16, 12, 16, 12),
            BorderThickness = new Thickness(1)
        };
        var previewAccent = new Border
        {
            Width = 24,
            Height = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            CornerRadius = new CornerRadius(1),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var previewTime = new TextBlock
        {
            Text = "23:41",
            FontSize = 28,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiLight,
            CharacterSpacing = 80
        };
        var previewDate = new TextBlock
        {
            Text = "Wednesday\nSeptember 2",
            FontSize = 12,
            CharacterSpacing = 20,
            LineHeight = 18
        };
        var previewStack = new StackPanel { Spacing = 4 };
        previewStack.Children.Add(previewAccent);
        previewStack.Children.Add(previewTime);
        previewStack.Children.Add(previewDate);
        preview.Child = previewStack;
        previewOuter.Child = preview;

        void RefreshPreview()
        {
            var opacity = Math.Clamp(opacityBox.Value, 0.35, 1.0);
            var radius = Math.Clamp(radiusBox.Value, 0, 40);
            previewOuter.Background = ThemePainter.Brush(draft.SurfaceSecondary, opacity * 0.55);
            previewOuter.BorderBrush = ThemePainter.Brush(draft.Border, 0.28);
            previewOuter.CornerRadius = new CornerRadius(radius + 2);
            preview.Background = ThemePainter.Brush(widgetBg.Text, opacity);
            preview.CornerRadius = new CornerRadius(Math.Max(8, radius - 2));
            preview.BorderBrush = ThemePainter.Brush(draft.Border, 0.55);
            previewAccent.Background = ThemePainter.Brush(accent.Text, 0.9);
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
            Text = "Applies to Clock, Music, Calendar, Text, AI, and Blocks.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
            FontSize = 12
        });
        panel.Children.Add(presetBox);
        panel.Children.Add(previewOuter);
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
            ShowHostStatus("Could not save layout.");
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
