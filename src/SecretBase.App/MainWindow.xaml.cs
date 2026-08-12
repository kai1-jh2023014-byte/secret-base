using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        // Do NOT assign a custom SystemBackdrop: WASDK 2.3 ABI cast between Microsoft.UI and
        // Windows.UI CompositionBrush projections FailFasts the process on connect.
        ExtendsContentIntoTitleBar = false;
        SystemBackdrop = null;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var hwnd = WindowNative.GetWindowHandle(this);
        _overlayTarget = new DesktopOverlayTarget(
            AppWindowId: AppWindow.Id.Value,
            WindowHandle: hwnd);

        _overlayService.ApplyChromelessWorkAreaOverlay(_overlayTarget);

        // When Windows activates us (e.g. click a widget), immediately return to HWND_BOTTOM
        // so normal applications stay above the desktop overlay layer.
        Activated += OnActivated;

        RootFrame.Navigate(typeof(DesktopPage), args);
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
    ITimeProvider TimeProvider);
