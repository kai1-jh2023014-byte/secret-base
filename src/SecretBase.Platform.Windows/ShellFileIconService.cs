using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Shell-associated icons via documented SHGetFileInfo / ExtractAssociatedIcon.
/// Writes PNG into a caller-provided cache directory (typically %LocalAppData%\SecretBase\icons).
/// </summary>
public sealed class ShellFileIconService : IFileIconService
{
    private readonly string _cacheDirectory;

    public ShellFileIconService(string? iconsDirectory = null)
    {
        _cacheDirectory = iconsDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase",
                "icons");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public string? TryGetCachedIconPath(string absoluteTargetPath, int sizePx = 48)
    {
        if (string.IsNullOrWhiteSpace(absoluteTargetPath))
        {
            return null;
        }

        var target = absoluteTargetPath.Trim();
        if (!Path.IsPathRooted(target))
        {
            return null;
        }

        var size = Math.Clamp(sizePx, 16, 256);
        var cachePath = GetCachePath(target, size);
        if (File.Exists(cachePath))
        {
            return cachePath;
        }

        try
        {
            using var bitmap = ExtractBitmap(target, size);
            if (bitmap is null)
            {
                return null;
            }

            bitmap.Save(cachePath, ImageFormat.Png);
            return File.Exists(cachePath) ? cachePath : null;
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? ExtractBitmap(string target, int size)
    {
        if (File.Exists(target))
        {
            try
            {
                using var associated = Icon.ExtractAssociatedIcon(target);
                if (associated is not null)
                {
                    return new Bitmap(associated.ToBitmap(), size, size);
                }
            }
            catch
            {
                // Fall through to SHGetFileInfo.
            }
        }

        var flags = NativeMethods.ShgfiIcon | NativeMethods.ShgfiLargeIcon;
        uint attributes = 0;
        if (Directory.Exists(target))
        {
            flags |= NativeMethods.ShgfiUseFileAttributes;
            attributes = NativeMethods.FileAttributeDirectory;
        }
        else if (!File.Exists(target))
        {
            return null;
        }

        var info = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(
            target,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            flags);

        if (result == nint.Zero || info.hIcon == nint.Zero)
        {
            return null;
        }

        try
        {
            using var temp = Icon.FromHandle(info.hIcon);
            using var clone = (Icon)temp.Clone();
            return new Bitmap(clone.ToBitmap(), size, size);
        }
        finally
        {
            _ = NativeMethods.DestroyIcon(info.hIcon);
        }
    }

    private string GetCachePath(string target, int size)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.ToLowerInvariant())))[..24];
        return Path.Combine(_cacheDirectory, $"{key}_{size}.png");
    }

    private static class NativeMethods
    {
        public const uint ShgfiIcon = 0x000000100;
        public const uint ShgfiLargeIcon = 0x000000000;
        public const uint ShgfiUseFileAttributes = 0x000000010;
        public const uint FileAttributeDirectory = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ShFileInfo
        {
            public nint hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern nint SHGetFileInfo(
            string pszPath,
            uint dwFileAttributes,
            ref ShFileInfo psfi,
            uint cbFileInfo,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(nint hIcon);
    }
}
