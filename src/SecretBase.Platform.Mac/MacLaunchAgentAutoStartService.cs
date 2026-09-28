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

        foreach (var candidate in EnumerateCandidateExecutablePaths())
        {
            var fileName = Path.GetFileName(candidate);
            if (LaunchAgentPlist.IsSupportedHostName(fileName) && File.Exists(candidate))
            {
                executablePath = Path.GetFullPath(candidate);
                return true;
            }
        }

        var processName = Path.GetFileName(_processPath() ?? string.Empty);
        if (LaunchAgentPlist.IsDotnetHost(processName))
        {
            errorMessage =
                "SecretBase.App.Mac was not found next to the running build. Build once, then enable Login again.";
            return false;
        }

        errorMessage = string.IsNullOrWhiteSpace(processName)
            ? "Could not resolve the application executable path."
            : $"Unexpected host executable '{processName}'. Auto-start supports SecretBase.App.Mac only.";
        return false;
    }

    private IEnumerable<string> EnumerateCandidateExecutablePaths()
    {
        var processPath = _processPath();
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            yield return processPath;
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            yield return Path.Combine(baseDir, "SecretBase.App.Mac");
        }

        var entry = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        var entryDir = string.IsNullOrWhiteSpace(entry) ? null : Path.GetDirectoryName(entry);
        if (!string.IsNullOrWhiteSpace(entryDir))
        {
            yield return Path.Combine(entryDir, "SecretBase.App.Mac");
        }
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
