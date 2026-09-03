using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Per-user macOS login auto-start via ~/Library/LaunchAgents. No elevation, no Dock changes.
/// </summary>
public sealed class MacLaunchAgentAutoStartService : IAutoStartService
{
    public const string DefaultLabel = LaunchAgentPlist.DefaultLabel;
    public const string AutoStartCommandLineSwitch = LaunchAgentPlist.AutoStartCommandLineSwitch;

    private readonly string _agentsDirectory;
    private readonly string _label;
    private readonly Func<string?> _processPath;
    private readonly bool _isSupported;

    public MacLaunchAgentAutoStartService(
        string? agentsDirectory = null,
        string? label = null,
        Func<string?>? processPath = null,
        bool? isSupported = null)
    {
        _label = string.IsNullOrWhiteSpace(label) ? DefaultLabel : label.Trim();
        _agentsDirectory = agentsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library",
                "LaunchAgents");
        _processPath = processPath ?? (() => Environment.ProcessPath);
        _isSupported = isSupported ?? OperatingSystem.IsMacOS();
    }

    public bool IsSupported => _isSupported;

    public AutoStartStatus GetStatus()
    {
        if (!IsSupported)
        {
            return new AutoStartStatus
            {
                IsRegistered = false,
                ErrorMessage = "Auto-start is only supported on macOS."
            };
        }

        try
        {
            var plistPath = GetPlistPath();
            if (!File.Exists(plistPath))
            {
                return new AutoStartStatus { IsRegistered = false };
            }

            var xml = File.ReadAllText(plistPath);
            var registeredExecutable = LaunchAgentPlist.TryReadExecutablePath(xml, out var parsed)
                ? parsed
                : null;
            var currentExecutable = TryResolveExecutablePath(out var currentPath, out _)
                ? currentPath
                : null;

            return new AutoStartStatus
            {
                IsRegistered = true,
                RegisteredCommand = registeredExecutable,
                PointsToCurrentExecutable = PathsEqual(registeredExecutable, currentExecutable)
            };
        }
        catch (Exception ex)
        {
            return new AutoStartStatus
            {
                IsRegistered = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public bool TryEnable(out string? errorMessage)
    {
        errorMessage = null;
        if (!IsSupported)
        {
            errorMessage = "Auto-start is only supported on macOS.";
            return false;
        }

        if (!TryResolveExecutablePath(out var executablePath, out errorMessage))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(_agentsDirectory);
            var xml = LaunchAgentPlist.Build(_label, executablePath);
            var temp = GetPlistPath() + ".tmp";
            File.WriteAllText(temp, xml);
            File.Copy(temp, GetPlistPath(), overwrite: true);
            File.Delete(temp);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryDisable(out string? errorMessage)
    {
        errorMessage = null;
        if (!IsSupported)
        {
            errorMessage = "Auto-start is only supported on macOS.";
            return false;
        }

        try
        {
            var plistPath = GetPlistPath();
            if (File.Exists(plistPath))
            {
                File.Delete(plistPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public bool TryGetStartupExecutablePath(out string executablePath, out string? errorMessage) =>
        TryResolveExecutablePath(out executablePath, out errorMessage);

    internal bool TryResolveExecutablePath(out string executablePath, out string? errorMessage)
    {
        executablePath = string.Empty;
        errorMessage = null;

        var processPath = _processPath();
        if (string.IsNullOrWhiteSpace(processPath))
        {
            errorMessage = "Could not resolve the application executable path.";
            return false;
        }

        var fileName = Path.GetFileName(processPath);
        if (LaunchAgentPlist.IsDotnetHost(fileName))
        {
            errorMessage =
                "Auto-start requires launching SecretBase.App.Mac directly. It is not available while using dotnet run.";
            return false;
        }

        if (!LaunchAgentPlist.IsSupportedHostName(fileName))
        {
            errorMessage = $"Unexpected host executable '{fileName}'. Auto-start supports SecretBase.App.Mac only.";
            return false;
        }

        if (!File.Exists(processPath))
        {
            errorMessage = "The application executable could not be found on disk.";
            return false;
        }

        executablePath = Path.GetFullPath(processPath);
        return true;
    }

    private string GetPlistPath() =>
        Path.Combine(_agentsDirectory, LaunchAgentPlist.FileNameForLabel(_label));

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.Ordinal);
    }
}
