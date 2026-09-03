using System.Runtime.InteropServices;
using SecretBase.Core;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// macOS host compatibility reader using public, managed APIs only.
/// </summary>
public sealed class MacCompatibilityService : ICompatibilityService
{
    public const string NotApplicableWindowsAppSdk = "n/a (macOS host)";

    public CompatibilityInfo GetCurrent()
    {
        var os = Environment.OSVersion;
        return new CompatibilityInfo(
            OsDescription: RuntimeInformation.OSDescription,
            OsVersion: os.Version.ToString(),
            OsArchitecture: RuntimeInformation.OSArchitecture.ToString(),
            DotNetVersion: RuntimeInformation.FrameworkDescription,
            WindowsAppSdkPackageVersion: NotApplicableWindowsAppSdk,
            AppVersion: AppInfo.Version);
    }
}
