using SecretBase.Platform.Windows;

namespace SecretBase.Platform.Windows.Tests;

public class AutoStartServiceTests
{
    [Theory]
    [InlineData(@"C:\Apps\SecretBase.App.exe", @"""C:\Apps\SecretBase.App.exe"" --autostart")]
    [InlineData(@"C:\Apps\Secret Base\SecretBase.App.exe", @"""C:\Apps\Secret Base\SecretBase.App.exe"" --autostart")]
    public void BuildStartupCommand_QuotesExecutableAndAddsSwitch(string executable, string expected)
    {
        Assert.Equal(expected, WindowsRegistryAutoStartService.BuildStartupCommand(executable));
    }

    [Theory]
    [InlineData(@"""C:\Apps\SecretBase.App.exe"" --autostart", @"C:\Apps\SecretBase.App.exe")]
    [InlineData(@"C:\Apps\SecretBase.App.exe --autostart", @"C:\Apps\SecretBase.App.exe")]
    public void TryParseExecutablePath_ExtractsExecutable(string command, string expected)
    {
        Assert.True(WindowsRegistryAutoStartService.TryParseExecutablePath(command, out var path));
        Assert.Equal(expected, path);
    }

    [Fact]
    public void GetStatus_OnNonWindows_ReturnsUnsupportedMessage()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var service = new WindowsRegistryAutoStartService($"SecretBase.Tests.{Guid.NewGuid():N}");
        Assert.False(service.IsSupported);
        var status = service.GetStatus();
        Assert.False(status.IsRegistered);
        Assert.Contains("Windows", status.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnumerateCandidateExecutablePaths_IncludesBaseDirectoryApphost()
    {
        var paths = WindowsRegistryAutoStartService.EnumerateCandidateExecutablePaths().ToList();
        Assert.Contains(
            paths,
            path => path.EndsWith("SecretBase.App.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsAppHostExecutable_RequiresExistingSecretBaseAppExe()
    {
        Assert.False(WindowsRegistryAutoStartService.IsAppHostExecutable(null));
        Assert.False(WindowsRegistryAutoStartService.IsAppHostExecutable("dotnet.exe"));
        Assert.False(WindowsRegistryAutoStartService.IsAppHostExecutable(
            Path.Combine(Path.GetTempPath(), "SecretBase.App.exe")));
    }

    [Fact]
    public void RegistryEnableDisable_RoundTrips_WithIsolatedValueName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var valueName = $"SecretBase.Tests.{Guid.NewGuid():N}";
        var service = new WindowsRegistryAutoStartService(valueName);
        Assert.True(service.IsSupported);

        try
        {
            Assert.False(service.GetStatus().IsRegistered);
            if (!service.TryGetStartupExecutablePath(out _, out var resolveError))
            {
                // Expected under dotnet test host — registry tests require SecretBase.App.exe host.
                Assert.Contains("SecretBase.App.exe", resolveError, StringComparison.OrdinalIgnoreCase);
                return;
            }

            Assert.True(service.TryEnable(out var enableError), enableError);
            var enabled = service.GetStatus();
            Assert.True(enabled.IsRegistered);
            Assert.True(enabled.PointsToCurrentExecutable);
            Assert.Contains(WindowsRegistryAutoStartService.AutoStartCommandLineSwitch, enabled.RegisteredCommand, StringComparison.Ordinal);

            Assert.True(service.TryDisable(out var disableError), disableError);
            Assert.False(service.GetStatus().IsRegistered);
        }
        finally
        {
            service.TryDisable(out _);
        }
    }
}
