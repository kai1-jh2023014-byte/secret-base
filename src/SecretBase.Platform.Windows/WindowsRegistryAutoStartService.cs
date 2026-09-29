using Microsoft.Win32;
using SecretBase.Core;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Per-user Windows logon startup via HKCU\...\Run. Visible in Windows Settings → Startup apps.
/// </summary>
public sealed class WindowsRegistryAutoStartService : IAutoStartService
{
    public const string DefaultRegistryValueName = AppInfo.ProductId;
    public const string AutoStartCommandLineSwitch = "--autostart";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private readonly string _registryValueName;

    public WindowsRegistryAutoStartService(string? registryValueName = null)
    {
        _registryValueName = string.IsNullOrWhiteSpace(registryValueName)
            ? DefaultRegistryValueName
            : registryValueName;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public AutoStartStatus GetStatus()
    {
        if (!IsSupported)
        {
            return new AutoStartStatus
            {
                IsRegistered = false,
                ErrorMessage = "Auto-start is only supported on Windows."
            };
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var command = key?.GetValue(_registryValueName) as string;
            if (string.IsNullOrWhiteSpace(command))
            {
                return new AutoStartStatus { IsRegistered = false };
            }

            var registeredExecutable = TryParseExecutablePath(command, out var parsed)
                ? parsed
                : null;
            var currentExecutable = TryResolveExecutablePath(out var currentPath, out _)
                ? currentPath
                : null;

            return new AutoStartStatus
            {
                IsRegistered = true,
                RegisteredCommand = command.Trim(),
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
            errorMessage = "Auto-start is only supported on Windows.";
            return false;
        }

        if (!TryResolveExecutablePath(out var executablePath, out errorMessage))
        {
            return false;
        }

        try
        {
            var command = BuildStartupCommand(executablePath);
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                errorMessage = "Could not open the Windows startup registry key.";
                return false;
            }

            key.SetValue(_registryValueName, command, RegistryValueKind.String);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        catch (IOException ex)
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
            errorMessage = "Auto-start is only supported on Windows.";
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return true;
            }

            key.DeleteValue(_registryValueName, throwOnMissingValue: false);
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

    internal static string BuildStartupCommand(string executablePath) =>
        $"{QuotePath(executablePath)} {AutoStartCommandLineSwitch}";

    internal static bool TryResolveExecutablePath(out string executablePath, out string? errorMessage)
    {
        executablePath = string.Empty;
        errorMessage = null;

        foreach (var candidate in EnumerateCandidateExecutablePaths())
        {
            if (IsAppHostExecutable(candidate))
            {
                executablePath = Path.GetFullPath(candidate);
                return true;
            }
        }

        var processName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        if (processName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
            || processName.Equals("testhost.exe", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage =
                "SecretBase.App.exe was not found next to the running build. Run build.ps1 / run.ps1 once, then enable Start at login again.";
            return false;
        }

        errorMessage = string.IsNullOrWhiteSpace(processName)
            ? "Could not resolve the application executable path."
            : $"Unexpected host executable '{processName}'. Auto-start supports SecretBase.App.exe only.";
        return false;
    }

    /// <summary>
    /// Prefers the live process path when it is already SecretBase.App.exe.
    /// Under <c>dotnet run</c> / run.ps1, falls back to the built apphost beside AppContext.BaseDirectory
    /// so login registration still points at a double-clickable exe.
    /// </summary>
    internal static IEnumerable<string> EnumerateCandidateExecutablePaths()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            yield return processPath;
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            yield return Path.Combine(baseDir, "SecretBase.App.exe");
        }

        var entry = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        var entryDir = string.IsNullOrWhiteSpace(entry) ? null : Path.GetDirectoryName(entry);
        if (!string.IsNullOrWhiteSpace(entryDir))
        {
            yield return Path.Combine(entryDir, "SecretBase.App.exe");
        }
    }

    internal static bool IsAppHostExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return fileName.Equals("SecretBase.App.exe", StringComparison.OrdinalIgnoreCase)
               && File.Exists(path);
    }

    internal static bool TryParseExecutablePath(string command, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();
        if (trimmed.StartsWith('"'))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote < 1)
            {
                return false;
            }

            executablePath = trimmed[1..endQuote];
            return true;
        }

        var space = trimmed.IndexOf(' ');
        executablePath = space < 0 ? trimmed : trimmed[..space];
        return !string.IsNullOrWhiteSpace(executablePath);
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string QuotePath(string path) => $"\"{path}\"";
}
