using System.Runtime.InteropServices;
using SecretBase.Core;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Windows host compatibility reader using public, managed APIs only.
/// </summary>
public sealed class WindowsCompatibilityService : ICompatibilityService
{
    private readonly string _windowsAppSdkPackageVersion;

    public WindowsCompatibilityService(string windowsAppSdkPackageVersion = "2.3.1")
    {
        _windowsAppSdkPackageVersion = windowsAppSdkPackageVersion;
    }

    public CompatibilityInfo GetCurrent()
    {
        var os = Environment.OSVersion;
        return new CompatibilityInfo(
            OsDescription: RuntimeInformation.OSDescription,
            OsVersion: os.Version.ToString(),
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            DotNetVersion: RuntimeInformation.FrameworkDescription,
            WindowsAppSdkPackageVersion: _windowsAppSdkPackageVersion,
            AppVersion: AppInfo.Version);
    }
}
