using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.App.Desktop;
using SecretBase.Core.Calendar;
using SecretBase.Core.Creative;
using SecretBase.Core.Time;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using WinRT.Interop;

namespace SecretBase.App;

public sealed partial class MainWindow : Window
{
    private readonly IDesktopOverlayService _overlayService;
    private readonly DesktopOverlayTarget _overlayTarget;

    public MainWindow(DesktopPageArgs args, IDesktopOverlayService overlayService)
    {
        InitializeComponent();

        _overlayService = overlayService;

        // Chromeless content — title bar removed via OverlappedPresenter in Platform.Windows.
        // Transparent backdrop uses Windows.UI.Composition brushes (not Microsoft.UI ABI cast).
        ExtendsContentIntoTitleBar = false;
        SystemBackdrop = new TransparentSystemBackdrop();
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var hwnd = WindowNative.GetWindowHandle(this);
        args.PathPicker?.SetOwnerWindow(hwnd);
        _overlayTarget = new DesktopOverlayTarget(
            AppWindowId: AppWindow.Id.Value,
            WindowHandle: hwnd);

        _overlayService.ApplyChromelessWorkAreaOverlay(_overlayTarget);

        // When Windows activates us (e.g. click a widget), immediately return to HWND_BOTTOM
        // so normal applications stay above the desktop overlay layer.
        // KeepBehind also reapplies the cached SetWindowRgn shape.
        Activated += OnActivated;

        // Page needs overlay target to push widget hit regions (SetWindowRgn) after layout.
        var pageArgs = args with
        {
            Overlay = _overlayService,
            OverlayTarget = _overlayTarget
        };
        RootFrame.Navigate(typeof(DesktopPage), pageArgs);

        // WinUI may finish creating DesktopChildSiteBridge after first navigate — re-sync soon.
        if (DispatcherQueue is not null)
        {
            _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (RootFrame.Content is DesktopPage page)
                {
                    page.RequestInteractiveRegionSync();
                }
            });
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        _overlayService.KeepBehindApplicationWindows(_overlayTarget);
    }
}

public sealed record DesktopPageArgs(
    IAppLogger Logger,
    ISafeExitService SafeExit,
    CompatibilityInfo Compatibility,
    ILayoutStore LayoutStore,
    IThemeStore ThemeStore,
    ITimeProvider TimeProvider,
    IDesktopOverlayService? Overlay = null,
    DesktopOverlayTarget? OverlayTarget = null,
    ITargetLaunchService? Launcher = null,
    IFileIconService? Icons = null,
    IBlockItemIntakeService? Intake = null,
    ISecureSecretStore? SecretStore = null,
    ICalendarAgendaCache? CalendarCache = null,
    IPathPickService? PathPicker = null,
    CreativeCommandService? CreativeCommands = null);
