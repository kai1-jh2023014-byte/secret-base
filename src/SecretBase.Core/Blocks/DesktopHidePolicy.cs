namespace SecretBase.Core.Blocks;

/// <summary>
/// Decides whether a user-dropped Desktop item should be relocated into Secret Base
/// (hide from Desktop) versus kept as a path reference. Never relocates install trees.
/// </summary>
public static class DesktopHidePolicy
{
    public static bool IsUnderRoot(string fullPath, string? root)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalized = HostPath.NormalizeSeparators(fullPath);
        var prefix = HostPath.NormalizeSeparators(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (prefix.Length == 0)
        {
            return false;
        }

        var withSep = prefix + (HostPath.IsPosixAbsolute(prefix) ? "/" : "\\");
        var comparison = HostPath.IsPosixAbsolute(prefix)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        return normalized.Equals(prefix, comparison)
               || normalized.StartsWith(withSep, comparison);
    }

    public static bool IsProtectedInstallPath(string fullPath, IEnumerable<string> protectedRoots)
    {
        foreach (var root in protectedRoots)
        {
            if (IsUnderRoot(fullPath, root))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Hide Desktop files/shortcuts (including Desktop .exe) by moving them.
    /// Folders and protected install locations stay as references.
    /// </summary>
    public static bool ShouldHideFromDesktop(
        string fullPath,
        bool isDirectory,
        IReadOnlyList<string> desktopRoots,
        IReadOnlyList<string> protectedRoots)
    {
        if (isDirectory || HostPath.IsMacAppBundle(fullPath))
        {
            return false;
        }

        if (IsProtectedInstallPath(fullPath, protectedRoots))
        {
            return false;
        }

        return desktopRoots.Any(root => IsUnderRoot(fullPath, root));
    }
}
