using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Shell-associated icons via documented SHGetFileInfo / SHGetImageList.
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
        var fromShell = TryExtractFromSystemImageList(target, size);
        if (fromShell is not null)
        {
            return fromShell;
        }

        if (File.Exists(target))
        {
            try
            {
                using var associated = Icon.ExtractAssociatedIcon(target);
                if (associated is not null)
                {
                    return ResizeHighQuality(associated.ToBitmap(), size);
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
            return ResizeHighQuality(clone.ToBitmap(), size);
        }
        finally
        {
            _ = NativeMethods.DestroyIcon(info.hIcon);
        }
    }

    private static Bitmap? TryExtractFromSystemImageList(string target, int size)
    {
        var flags = NativeMethods.ShgfiSysIconIndex | NativeMethods.ShgfiUseFileAttributes;
        uint attributes = NativeMethods.FileAttributeNormal;
        if (Directory.Exists(target))
        {
            attributes = NativeMethods.FileAttributeDirectory;
        }
        else if (!File.Exists(target) && !Directory.Exists(target))
        {
            // Still try by extension / path for missing targets that have a type icon.
            attributes = NativeMethods.FileAttributeNormal;
        }

        var info = new NativeMethods.ShFileInfo();
        var result = NativeMethods.SHGetFileInfo(
            target,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<NativeMethods.ShFileInfo>(),
            flags);
        if (result == nint.Zero)
        {
            return null;
        }

        var listId = size >= 128
            ? NativeMethods.ShilJumbo
            : size >= 48
                ? NativeMethods.ShilExtraLarge
                : NativeMethods.ShilLarge;

        var iid = NativeMethods.IidIImageList;
        var hr = NativeMethods.SHGetImageList(listId, ref iid, out var imageList);
        if (hr != 0 || imageList is null)
        {
            return null;
        }

        nint hIcon = nint.Zero;
        try
        {
            hr = imageList.GetIcon(info.iIcon, NativeMethods.IldTransparent, ref hIcon);
            if (hr != 0 || hIcon == nint.Zero)
            {
                return null;
            }

            using var temp = Icon.FromHandle(hIcon);
            using var clone = (Icon)temp.Clone();
            return ResizeHighQuality(clone.ToBitmap(), size);
        }
        finally
        {
            if (hIcon != nint.Zero)
            {
                _ = NativeMethods.DestroyIcon(hIcon);
            }

            if (Marshal.IsComObject(imageList))
            {
                _ = Marshal.ReleaseComObject(imageList);
            }
        }
    }

    private static Bitmap ResizeHighQuality(Bitmap source, int size)
    {
        if (source.Width == size && source.Height == size)
        {
            return new Bitmap(source);
        }

        var dest = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dest))
        {
            g.Clear(Color.Transparent);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, size, size));
        }

        source.Dispose();
        return dest;
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
        public const uint ShgfiSysIconIndex = 0x000004000;
        public const uint ShgfiUseFileAttributes = 0x000000010;
        public const uint FileAttributeDirectory = 0x00000010;
        public const uint FileAttributeNormal = 0x00000080;
        public const int ShilLarge = 0x0;
        public const int ShilExtraLarge = 0x2;
        public const int ShilJumbo = 0x4;
        public const int IldTransparent = 0x00000001;

        public static readonly Guid IidIImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

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

        [DllImport("shell32.dll", EntryPoint = "#727")]
        public static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(nint hIcon);

        [ComImport]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IImageList
        {
            [PreserveSig]
            int Add(nint hbmImage, nint hbmMask, ref int pi);

            [PreserveSig]
            int ReplaceIcon(int i, nint hicon, ref int pi);

            [PreserveSig]
            int SetOverlayImage(int iImage, int iOverlay);

            [PreserveSig]
            int Replace(int i, nint hbmImage, nint hbmMask);

            [PreserveSig]
            int AddMasked(nint hbmImage, int crMask, ref int pi);

            [PreserveSig]
            int Draw(ref Imagelistdrawparams pimldp);

            [PreserveSig]
            int Remove(int i);

            [PreserveSig]
            int GetIcon(int i, int flags, ref nint picon);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Imagelistdrawparams
        {
            public int cbSize;
            public nint himl;
            public int i;
            public nint hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }
    }
}
