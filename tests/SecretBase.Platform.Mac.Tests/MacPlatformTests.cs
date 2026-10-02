using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Mac;

namespace SecretBase.Platform.Mac.Tests;

public class LaunchAgentPlistTests
{
    [Fact]
    public void Build_IncludesExecutableAndAutostartSwitch()
    {
        var xml = LaunchAgentPlist.Build("com.secretbase.app", "/opt/SecretBase.App.Mac");
        Assert.Contains("<string>com.secretbase.app</string>", xml);
        Assert.Contains("<string>/opt/SecretBase.App.Mac</string>", xml);
        Assert.Contains("<string>--autostart</string>", xml);
        Assert.Contains("<string>Aqua</string>", xml);
        Assert.DoesNotContain("KeepAlive", xml);

        Assert.True(LaunchAgentPlist.TryReadExecutablePath(xml, out var path));
        Assert.Equal("/opt/SecretBase.App.Mac", path);
    }

    [Fact]
    public void Build_EscapesXmlSpecialCharactersInPath()
    {
        var xml = LaunchAgentPlist.Build("com.secretbase.app", "/tmp/Secret & Base/SecretBase.App.Mac");
        Assert.Contains("&amp;", xml);
        Assert.True(LaunchAgentPlist.TryReadExecutablePath(xml, out var path));
        Assert.Equal("/tmp/Secret & Base/SecretBase.App.Mac", path);
    }
}

public class MacLaunchAgentAutoStartServiceTests
{
    [Fact]
    public void GetStatus_WhenUnsupported_ReturnsMessage()
    {
        var service = new MacLaunchAgentAutoStartService(
            agentsDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            isSupported: false);
        Assert.False(service.IsSupported);
        var status = service.GetStatus();
        Assert.False(status.IsRegistered);
        Assert.Contains("macOS", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnableDisable_RoundTrips_WithFakeExecutable()
    {
        var root = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        var agents = Path.Combine(root, "LaunchAgents");
        var exe = Path.Combine(root, "SecretBase.App.Mac");
        Directory.CreateDirectory(root);
        File.WriteAllText(exe, "placeholder");

        var service = new MacLaunchAgentAutoStartService(
            agentsDirectory: agents,
            label: "com.secretbase.tests",
            processPath: () => exe,
            isSupported: true);

        try
        {
            Assert.False(service.GetStatus().IsRegistered);
            Assert.True(service.TryEnable(out var enableError), enableError);
            var enabled = service.GetStatus();
            Assert.True(enabled.IsRegistered);
            Assert.True(enabled.PointsToCurrentExecutable);
            Assert.Equal(exe, enabled.RegisteredCommand);

            Assert.True(service.TryDisable(out var disableError), disableError);
            Assert.False(service.GetStatus().IsRegistered);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void TryEnable_RejectsDotnetHost_WhenApphostMissing()
    {
        var service = new MacLaunchAgentAutoStartService(
            agentsDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            processPath: () => "/usr/local/share/dotnet/dotnet",
            isSupported: true);

        Assert.False(service.TryEnable(out var error));
        Assert.Contains("SecretBase.App.Mac", error, StringComparison.OrdinalIgnoreCase);
    }
}

public class FileLockSingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_SecondInstance_FailsWhileFirstHeld()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var first = new FileLockSingleInstanceGuard(dir);
            using var second = new FileLockSingleInstanceGuard(dir);
            Assert.True(first.TryAcquire());
            Assert.False(second.TryAcquire());
            first.Dispose();
            Assert.True(second.TryAcquire());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class MacSecureSecretStoreTests
{
    [Fact]
    public void FileBackedStore_RoundTripsWithoutLoggingValue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        var store = new MacSecureSecretStore(new FileBackedSecretStore(dir));
        try
        {
            Assert.False(store.TryGetSecret("Assistant/OpenAI/ApiKey", out _));
            store.SetSecret("Assistant/OpenAI/ApiKey", "sk-test-not-for-git");
            Assert.True(store.TryGetSecret("Assistant/OpenAI/ApiKey", out var value));
            Assert.Equal("sk-test-not-for-git", value);
            store.DeleteSecret("Assistant/OpenAI/ApiKey");
            Assert.False(store.TryGetSecret("Assistant/OpenAI/ApiKey", out _));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

public class MacTargetLaunchServiceTests
{
    [Fact]
    public void TryLaunch_RejectsRelativePath()
    {
        var service = new MacTargetLaunchService((_, _) => new TargetLaunchResult(true, null));
        var result = service.TryLaunch(new TargetLaunchRequest("Applications/Foo.app", "Application", "Foo"));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void TryLaunch_UsesOpenWithValidatedAbsolutePath()
    {
        string? fileName = null;
        IReadOnlyList<string>? args = null;
        var service = new MacTargetLaunchService((exe, arguments) =>
        {
            fileName = exe;
            args = arguments;
            return new TargetLaunchResult(true, null);
        });

        var result = service.TryLaunch(new TargetLaunchRequest("/Applications/Safari.app", "Application", "Safari"));
        Assert.True(result.Succeeded);
        Assert.Equal(MacTargetLaunchService.OpenExecutable, fileName);
        Assert.Equal(new[] { "/Applications/Safari.app" }, args);
    }
}

public class MacCursorLaunchServiceTests
{
    [Fact]
    public void TryOpenFolder_UsesOpenDashA_ForAppBundle()
    {
        var folder = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string? fileName = null;
        IReadOnlyList<string>? args = null;
        try
        {
            var service = new MacCursorLaunchService(
                pathEnvironment: null,
                homeDirectory: "/Users/demo",
                applicationsDirectory: "/Applications",
                localAppData: null,
                fileExists: p => p.EndsWith("Cursor.app", StringComparison.Ordinal),
                start: (exe, arguments) =>
                {
                    fileName = exe;
                    args = arguments;
                    return new CursorLaunchResult(true, null);
                });

            Assert.True(service.IsAvailable);
            var result = service.TryOpenFolder(folder);
            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(MacTargetLaunchService.OpenExecutable, fileName);
            Assert.Equal("-a", args![0]);
            Assert.Contains("Cursor.app", args[1], StringComparison.Ordinal);
            Assert.Equal("--args", args[2]);
            Assert.Equal(folder, args[3]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

public class MacCompatibilityServiceTests
{
    [Fact]
    public void GetCurrent_MarksWindowsAppSdkNotApplicable()
    {
        var info = new MacCompatibilityService().GetCurrent();
        Assert.Equal(MacCompatibilityService.NotApplicableWindowsAppSdk, info.WindowsAppSdkPackageVersion);
        Assert.Equal("0.6.0", info.AppVersion);
    }
}

public class MacBlockItemIntakeServiceTests
{
    [Fact]
    public void AppBundle_StaysAsPathReference()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var service = new MacBlockItemIntakeService(dir);
        var result = service.TryIntake("/Applications/Safari.app", Guid.NewGuid(), Guid.NewGuid());
        Assert.True(result.Succeeded);
        Assert.False(result.MovedFromSource);
        Assert.Equal("/Applications/Safari.app", result.TargetPath);
    }

    [Fact]
    public void DesktopFile_IsMovedAndRestored()
    {
        var root = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(root, "Desktop");
        var storage = Path.Combine(root, "block-items");
        Directory.CreateDirectory(desktop);
        var source = Path.Combine(desktop, "Notes.txt");
        File.WriteAllText(source, "hello");
        try
        {
            var service = new MacBlockItemIntakeService(storage, [desktop], ["/Applications"]);
            var result = service.TryIntake(source, Guid.NewGuid(), Guid.NewGuid());
            Assert.True(result.Succeeded);
            Assert.True(result.MovedFromSource);
            Assert.False(File.Exists(source));
            Assert.True(service.TryRestoreToDesktop(result.TargetPath, result.DesktopOriginPath, out var restored, out var error));
            Assert.Null(error);
            Assert.True(File.Exists(restored));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

public class MacHttpsLauncherTests
{
    [Fact]
    public void TryOpen_RejectsNonHttp()
    {
        Assert.False(MacHttpsLauncher.TryOpen("file:///etc/passwd", (_, _) => true));
        Assert.False(MacHttpsLauncher.TryOpen("javascript:alert(1)", (_, _) => true));
    }

    [Fact]
    public void TryOpen_AllowsHttps()
    {
        string? opened = null;
        var ok = MacHttpsLauncher.TryOpen("https://example.com/a", (_, args) =>
        {
            opened = args[0];
            return true;
        });
        Assert.True(ok);
        Assert.Equal("https://example.com/a", opened);
    }
}
