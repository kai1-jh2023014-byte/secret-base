using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SecretBase.Core;
using SecretBase.Core.Desktop;
using SecretBase.Infrastructure.Logging;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App;

public sealed partial class DesktopPage : Page
{
    private IAppLogger? _logger;
    private ISafeExitService? _safeExit;

    public DesktopPage()
    {
        InitializeComponent();
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

        var layout = new DesktopLayout { RoomId = RoomId.DefaultRoomId };
        BrandText.Text = AppInfo.Name;
        StatusText.Text =
            $"Room: {layout.RoomId} · App {args.Compatibility.AppVersion} · " +
            $"OS {args.Compatibility.OsVersion} · .NET {args.Compatibility.DotNetVersion} · " +
            $"WASDK {args.Compatibility.WindowsAppSdkPackageVersion}";

        _logger.Info("desktop", $"Desktop shown for room '{layout.RoomId}'.");
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _logger?.Info("desktop", "User chose Exit to Windows Desktop.");
        _safeExit?.RequestExit();
    }
}
