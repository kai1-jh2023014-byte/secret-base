namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Runtime compatibility snapshot for logging and future Compatibility Checks.
/// Collected via Platform adapters — never from Core directly.
/// </summary>
public sealed record CompatibilityInfo(
    string OsDescription,
    string OsVersion,
    string OsArchitecture,
    string DotNetVersion,
    string WindowsAppSdkPackageVersion,
    string AppVersion);
