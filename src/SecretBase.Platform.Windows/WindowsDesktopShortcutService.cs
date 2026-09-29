using System.Runtime.InteropServices;
using SecretBase.Core;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Creates per-user Start Menu and Desktop shortcuts to SecretBase.App.exe.
/// Uses WScript.Shell COM (same as Explorer) — no elevation, no installer.
/// </summary>
public sealed class WindowsDesktopShortcutService
{
    public const string ShortcutFileName = AppInfo.Name + ".lnk";

    public bool IsSupported => OperatingSystem.IsWindows();

    public bool TryCreateLaunchers(string executablePath, out string? errorMessage, out string? detail)
    {
        errorMessage = null;
        detail = null;
        if (!IsSupported)
        {
            errorMessage = "Shortcuts are only supported on Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            errorMessage = "SecretBase.App.exe was not found. Build the app first (build.ps1 or run.ps1).";
            return false;
        }

        var exe = Path.GetFullPath(executablePath);
        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(workDir))
        {
            errorMessage = "Could not resolve the application folder.";
            return false;
        }

        try
        {
            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs",
                ShortcutFileName);
            var desktop = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                ShortcutFileName);

            CreateShortcut(startMenu, exe, workDir, "Secret Base — personal desktop overlay");
            CreateShortcut(desktop, exe, workDir, "Secret Base — personal desktop overlay");

            detail = $"Start Menu and Desktop shortcuts point to:{Environment.NewLine}{exe}";
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available on this PC.");
        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Could not create WScript.Shell.");
            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);
            if (shortcut is null)
            {
                throw new InvalidOperationException("Could not create the shortcut object.");
            }

            var shortcutType = shortcut.GetType();
            shortcutType.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath]);
            shortcutType.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, shortcut, [workingDirectory]);
            shortcutType.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, shortcut, [description]);
            shortcutType.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, shortcut, [targetPath + ",0"]);
            shortcutType.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, shortcut, null);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell is not null && Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
