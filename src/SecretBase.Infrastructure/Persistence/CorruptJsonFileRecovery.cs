using SecretBase.Infrastructure.Logging;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>
/// Backs up unreadable JSON configuration files before restoring safe defaults.
/// </summary>
internal static class CorruptJsonFileRecovery
{
    public static string? TryBackupCorruptFile(string path, IAppLogger? logger, string category)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HHmmss'Z'");
            var extension = Path.GetExtension(path);
            var stem = path[..^extension.Length];
            var backupPath = $"{stem}.corrupt-{stamp}{extension}";

            File.Move(path, backupPath, overwrite: false);
            logger?.Warn(category, $"Configuration file was invalid JSON: {Path.GetFileName(path)}");
            logger?.Info(category, $"Corrupt file backed up to {Path.GetFileName(backupPath)}");
            return backupPath;
        }
        catch (Exception ex)
        {
            logger?.Warn(category, $"Could not back up corrupt file '{Path.GetFileName(path)}': {ex.Message}");
            return null;
        }
    }
}
