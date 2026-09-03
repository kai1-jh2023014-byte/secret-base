using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Single-instance guard using an exclusive file lock (no mutex). Safe on macOS and Linux tests.
/// </summary>
public sealed class FileLockSingleInstanceGuard : ISingleInstanceGuard
{
    public const string DefaultLockFileName = "instance.lock";

    private readonly string _lockPath;
    private FileStream? _stream;
    private bool _held;

    public FileLockSingleInstanceGuard(string? lockDirectory = null, string? lockFileName = null)
    {
        var directory = lockDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase");
        Directory.CreateDirectory(directory);
        var name = string.IsNullOrWhiteSpace(lockFileName) ? DefaultLockFileName : lockFileName;
        _lockPath = Path.Combine(directory, name);
    }

    public bool IsHeld => _held;

    public bool TryAcquire()
    {
        if (_held)
        {
            return true;
        }

        try
        {
            _stream = new FileStream(
                _lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            _held = true;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_held)
        {
            return;
        }

        try
        {
            _stream?.Dispose();
        }
        finally
        {
            _stream = null;
            _held = false;
        }
    }
}
