using Microsoft.UI.Xaml;
using SecretBase.Infrastructure.Logging;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(IAppLogger logger, ISafeExitService safeExit, CompatibilityInfo compatibility)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(DesktopPage), new DesktopPageArgs(logger, safeExit, compatibility));
    }
}

public sealed record DesktopPageArgs(
    IAppLogger Logger,
    ISafeExitService SafeExit,
    CompatibilityInfo Compatibility);
