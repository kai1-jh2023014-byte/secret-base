namespace SecretBase.Core;

/// <summary>
/// Path shape helpers that do not call OS APIs. Windows drive/UNC and POSIX absolute
/// forms are both valid so Core validation works on Windows and macOS hosts.
/// </summary>
public static class HostPath
{
    public static bool IsWindowsDriveAbsolute(string path) =>
        path.Length >= 3
        && char.IsLetter(path[0])
        && path[1] == ':'
        && (path[2] == '\\' || path[2] == '/');

    public static bool IsUncAbsolute(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) && path.Length > 3;

    public static bool IsPosixAbsolute(string path) =>
        path.StartsWith('/') && path.Length > 1;

    public static bool IsAbsolute(string path) =>
        IsWindowsDriveAbsolute(path) || IsUncAbsolute(path) || IsPosixAbsolute(path);

    public static string NormalizeSeparators(string path)
    {
        if (IsWindowsDriveAbsolute(path) || IsUncAbsolute(path))
        {
            return path.Replace('/', '\\');
        }

        if (IsPosixAbsolute(path))
        {
            return path.Replace('\\', '/');
        }

        return path;
    }

    public static string GetFileName(string path)
    {
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var idx = normalized.LastIndexOf('/');
        return idx >= 0 && idx < normalized.Length - 1 ? normalized[(idx + 1)..] : normalized;
    }

    public static string GetExtension(string path)
    {
        var name = GetFileName(path);
        var idx = name.LastIndexOf('.');
        return idx >= 0 ? name[idx..] : string.Empty;
    }

    public static bool IsMacAppBundle(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        return GetExtension(trimmed).Equals(".app", StringComparison.OrdinalIgnoreCase);
    }
}
