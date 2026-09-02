using SecretBase.Infrastructure.Logging;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>
/// Backs up unreadable JSON configuration files before restoring safe defaults.
/// </summary>
internal static class CorruptJsonFileRecovery
{
    /// <summary>Test-only UTC clock override for deterministic backup collision scenarios.</summary>
    internal static Func<DateTime>? UtcNowOverrideForTests;

    private static readonly object TestClockGate = new();

    internal static void SetTestUtcNowForTests(DateTime utcInstant)
    {
        lock (TestClockGate)
        {
            UtcNowOverrideForTests = () => utcInstant;
        }
    }

    internal static void ClearTestUtcNowForTests()
    {
        lock (TestClockGate)
        {
            UtcNowOverrideForTests = null;
        }
    }

    public static string? TryBackupCorruptFile(string path, IAppLogger? logger, string category)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var stamp = GetUtcNow().ToString("yyyy-MM-dd'T'HHmmss'Z'");
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

    private static DateTime GetUtcNow()
    {
        lock (TestClockGate)
        {
            return UtcNowOverrideForTests?.Invoke() ?? DateTime.UtcNow;
        }
    }
}
