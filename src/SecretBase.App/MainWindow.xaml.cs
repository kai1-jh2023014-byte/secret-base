using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.App.Desktop;
using SecretBase.Core.Ai;
using SecretBase.Core.Apps;
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

        // When Windows activates us (e.g. click a widget), park just above the shell desktop
        // so normal applications stay above the overlay and the wallpaper stays behind it.
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

    public void PresentExistingInstance()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        _ = ShowWindow(hwnd, SwRestore);
        Activate();
        // RDP / monitor changes often leave the host on a stale work area — resync.
        _overlayService.ApplyChromelessWorkAreaOverlay(_overlayTarget);
        if (RootFrame.Content is DesktopPage page)
        {
            page.HandleDisplayMetricsChanged();
        }

        _overlayService.KeepBehindApplicationWindows(_overlayTarget);
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        // Re-fit to current DisplayArea.WorkArea when returning from Remote Desktop, etc.
        _overlayService.ApplyChromelessWorkAreaOverlay(_overlayTarget);
        if (RootFrame.Content is DesktopPage page)
        {
            page.HandleDisplayMetricsChanged();
        }

        _overlayService.KeepBehindApplicationWindows(_overlayTarget);
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);
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
    ICustomIconService? CustomIcons = null,
    CreativeCommandService? CreativeCommands = null,
    ICursorLaunchService? CursorLaunch = null,
    AiCommandService? AiCommands = null,
    AppCommandService? AppCommands = null,
    IAutoStartService? AutoStart = null,
    IAppLaunchSettingsStore? LaunchSettingsStore = null);
