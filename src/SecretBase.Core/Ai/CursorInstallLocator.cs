namespace SecretBase.Core.Ai;

/// <summary>
/// Resolves Cursor install location without launching processes.
/// Uses PATH + %LocalAppData%\Programs\… via injected folders (no hardcoded usernames).
/// </summary>
public static class CursorInstallLocator
{
    public static string? TryResolve(
        string? pathEnvironment,
        string? localAppData,
        Func<string, bool>? fileExists = null)
    {
        var exists = fileExists ?? File.Exists;
        var fromPath = FindOnPath(pathEnvironment, exists);
        if (fromPath is not null)
        {
            return fromPath;
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            return null;
        }

        foreach (var relative in new[]
                 {
                     Path.Combine("Programs", "cursor", "Cursor.exe"),
                     Path.Combine("Programs", "Cursor", "Cursor.exe")
                 })
        {
            var candidate = Path.Combine(localAppData, relative);
            if (exists(candidate))
            {
                return candidate;
            }
        }

        return null;
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
