namespace SecretBase.Core.Ai;

/// <summary>
/// Resolves Cursor install location without launching processes.
/// Uses PATH + known install folders via injected roots (no hardcoded usernames).
/// </summary>
public static class CursorInstallLocator
{
    public static string? TryResolve(
        string? pathEnvironment,
        string? localAppData,
        Func<string, bool>? fileExists = null) =>
        TryResolve(pathEnvironment, localAppData, fileExists, homeDirectory: null, applicationsDirectory: null);

    public static string? TryResolve(
        string? pathEnvironment,
        string? localAppData,
        Func<string, bool>? fileExists,
        string? homeDirectory,
        string? applicationsDirectory)
    {
        var exists = fileExists ?? File.Exists;
        var fromPath = FindOnPath(pathEnvironment, exists);
        if (fromPath is not null)
        {
            return fromPath;
        }

        foreach (var candidate in EnumerateKnownInstallPaths(localAppData, homeDirectory, applicationsDirectory))
        {
            if (exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static IEnumerable<string> EnumerateKnownInstallPaths(
        string? localAppData,
        string? homeDirectory,
        string? applicationsDirectory)
    {
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe");
            yield return Path.Combine(localAppData, "Programs", "Cursor", "Cursor.exe");
        }

        var appsRoot = string.IsNullOrWhiteSpace(applicationsDirectory)
            ? "/Applications"
            : applicationsDirectory;
        yield return Path.Combine(appsRoot, "Cursor.app", "Contents", "MacOS", "Cursor");
        yield return Path.Combine(appsRoot, "Cursor.app");

        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            yield return Path.Combine(homeDirectory, "Applications", "Cursor.app", "Contents", "MacOS", "Cursor");
            yield return Path.Combine(homeDirectory, "Applications", "Cursor.app");
        }
    }

    private static string? FindOnPath(string? pathEnv, Func<string, bool> exists)
    {
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        var extensions = new[] { ".cmd", ".exe", ".bat", "" };
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var name in new[] { "cursor", "Cursor" })
            {
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir.Trim().Trim('"'), name + ext);
                    if (exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>Quotes a single process argument (folder path only). Same rules on Windows and macOS.</summary>
    public static bool TryQuoteProcessArgument(string value, out string quoted, out string error) =>
        TryQuoteWindowsArgument(value, out quoted, out error);

    /// <summary>Quotes a single Windows process argument (folder path only).</summary>
    public static bool TryQuoteWindowsArgument(string value, out string quoted, out string error)
    {
        quoted = string.Empty;
        error = string.Empty;
        if (value.Contains('"'))
        {
            error = "Folder path contains invalid characters.";
            return false;
        }

        if (value.Length == 0)
        {
            quoted = "\"\"";
            return true;
        }

        quoted = value.IndexOfAny([' ', '\t']) < 0 ? value : "\"" + value + "\"";
        return true;
    }
}
