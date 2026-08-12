using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.App.Desktop;
using SecretBase.Core.Time;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;
using WinRT.Interop;

namespace SecretBase.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(DesktopPageArgs args, IDesktopOverlayService overlayService)
    {
        InitializeComponent();

        // Chromeless content — title bar removed via OverlappedPresenter in Platform.Windows.
        ExtendsContentIntoTitleBar = false;
        SystemBackdrop = new TransparentSystemBackdrop();
        AppWindow.SetIcon("Assets/AppIcon.ico");

        var hwnd = WindowNative.GetWindowHandle(this);
        overlayService.ApplyChromelessWorkAreaOverlay(
            new DesktopOverlayTarget(
                AppWindowId: AppWindow.Id.Value,
                WindowHandle: hwnd));

        RootFrame.Navigate(typeof(DesktopPage), args);
    }
}

public sealed record DesktopPageArgs(
    IAppLogger Logger,
    ISafeExitService SafeExit,
    CompatibilityInfo Compatibility,
    ILayoutStore LayoutStore,
    IThemeStore ThemeStore,
    ITimeProvider TimeProvider);
