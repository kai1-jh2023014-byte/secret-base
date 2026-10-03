using System.Runtime.InteropServices;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Win32 message box that does not depend on WinUI. A ContentDialog on the overlay
/// can fail to appear, which looks the same as the process closing immediately.
/// </summary>
public static class WindowsUserNotice
{
    public static void Show(string message, string title = "Secret Base")
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _ = MessageBoxW(nint.Zero, message, title, MbOk | MbIconWarning | MbSetForeground | MbTopMost);
    }

    private const uint MbOk = 0x00000000;
    private const uint MbIconWarning = 0x00000030;
    private const uint MbSetForeground = 0x00010000;
    private const uint MbTopMost = 0x00040000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(nint hWnd, string text, string caption, uint type);
}
