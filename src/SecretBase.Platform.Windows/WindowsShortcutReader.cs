using System.Runtime.InteropServices;

namespace SecretBase.Platform.Windows;

/// <summary>Target, arguments, and "Start in" folder stored in a .lnk file.</summary>
public readonly record struct WindowsShortcutInfo(
    string TargetPath,
    string? Arguments,
    string? WorkingDirectory);

/// <summary>
/// Reads a user shortcut through WScript.Shell (the same COM Explorer uses to create .lnk files).
/// </summary>
public static class WindowsShortcutReader
{
    public static bool TryRead(string shortcutPath, out WindowsShortcutInfo info)
    {
        info = default;
        if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath))
        {
            return false;
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
        {
            return false;
        }

        object? shell = null;
        object? shortcut = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                binder: null,
                target: shell,
                args: [shortcutPath]);
            if (shortcut is null)
            {
                return false;
            }

            var shortcutType = shortcut.GetType();
            var target = ReadString(shortcutType, shortcut, "TargetPath");
            if (string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            info = new WindowsShortcutInfo(
                target,
                ReadString(shortcutType, shortcut, "Arguments"),
                ReadString(shortcutType, shortcut, "WorkingDirectory"));
            return true;
        }
        catch (COMException)
        {
            return false;
        }
        catch (System.Reflection.TargetInvocationException)
        {
            return false;
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

    private static string? ReadString(Type shortcutType, object shortcut, string property)
    {
        var value = shortcutType.InvokeMember(
            property,
            System.Reflection.BindingFlags.GetProperty,
            binder: null,
            target: shortcut,
            args: null);
        return value as string;
    }
}
