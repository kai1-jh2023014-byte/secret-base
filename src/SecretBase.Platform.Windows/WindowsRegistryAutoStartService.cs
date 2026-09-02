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

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            errorMessage = "Could not resolve the application executable path.";
            return false;
        }

        var fileName = Path.GetFileName(processPath);
        if (fileName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage =
                "Auto-start requires launching SecretBase.App.exe directly. It is not available while using dotnet run or run.ps1.";
            return false;
        }

        if (!fileName.Equals("SecretBase.App.exe", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = $"Unexpected host executable '{fileName}'. Auto-start supports SecretBase.App.exe only.";
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
