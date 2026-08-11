using Microsoft.UI.Xaml;
using SecretBase.Core.Time;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(DesktopPageArgs args)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

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
