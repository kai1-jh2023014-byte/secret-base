namespace SecretBase.Core.Blocks;

/// <summary>
/// Per-Block folder layout under Secret Base storage.
/// Each Block gets a discoverable directory; intake keeps original file names.
/// </summary>
public static class BlockItemStorage
{
    public const string ReadmeFileName = "README-SecretBase.txt";

    /// <summary>
    /// Resolves <c>{storageRoot}/{SafeBlockName} ({id8})/</c>, creating it when missing.
    /// Reuses an existing folder that ends with the same block-id marker if the Block was renamed.
    /// </summary>
    public static string EnsureBlockDirectory(string storageRoot, Guid blockId, string? blockDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        Directory.CreateDirectory(storageRoot);

        var marker = blockId.ToString("N")[..8];
        var existing = FindExistingBlockDirectory(storageRoot, marker);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            WriteReadmeIfMissing(existing, blockDisplayName, blockId);
            return existing;
        }

        var safe = SanitizeFolderName(blockDisplayName);
        var folderName = $"{safe} ({marker})";
        var path = Path.Combine(storageRoot, folderName);
        Directory.CreateDirectory(path);
        WriteReadmeIfMissing(path, blockDisplayName, blockId);
        return path;
    }

    public static string? FindExistingBlockDirectory(string storageRoot, string idMarker8)
    {
        if (!Directory.Exists(storageRoot) || string.IsNullOrWhiteSpace(idMarker8))
        {
            return null;
        }

        var suffix = $" ({idMarker8})";
        foreach (var dir in Directory.EnumerateDirectories(storageRoot))
        {
            var name = Path.GetFileName(dir);
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }

            // Legacy layout: block-items/{fullGuidN}/
            if (name.Length == 32
                && name.StartsWith(idMarker8, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParseExact(name, "N", out _))
            {
                return dir;
            }
        }

        return null;
    }

    public static string SanitizeFolderName(string? name)
    {
        var trimmed = string.IsNullOrWhiteSpace(name) ? "Block" : name.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = trimmed.Select(c => invalid.Contains(c) || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'
            ? '_'
            : c).ToArray();
        var safe = new string(chars).Trim(' ', '.');
        if (safe.Length == 0)
        {
            safe = "Block";
        }

        return safe.Length > 60 ? safe[..60].TrimEnd(' ', '.') : safe;
    }

    public static void WriteReadmeIfMissing(string blockDirectory, string? blockDisplayName, Guid blockId)
    {
        try
        {
            var readme = Path.Combine(blockDirectory, ReadmeFileName);
            if (File.Exists(readme))
            {
                return;
            }

            var title = string.IsNullOrWhiteSpace(blockDisplayName) ? "Block" : blockDisplayName.Trim();
            var body =
                $"Secret Base Block folder\r\n" +
                $"========================\r\n" +
                $"Block: {title}\r\n" +
                $"Id: {blockId:D}\r\n" +
                $"\r\n" +
                $"Desktop shortcuts/files dropped into this Block are moved here\r\n" +
                $"(original file names are kept). Use \"Return to Desktop\" in Secret Base\r\n" +
                $"or \"Open Block folder\" from the Block menu to find them.\r\n";
            File.WriteAllText(readme, body);
        }
        catch
        {
            // Non-fatal — intake should still succeed without the readme.
        }
    }
}
