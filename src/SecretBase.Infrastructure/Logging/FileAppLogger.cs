using System.Text;

namespace SecretBase.Infrastructure.Logging;

/// <summary>
/// Append-only text logger under LocalAppData. No secrets by convention — callers must redact.
/// </summary>
public sealed class FileAppLogger : IAppLogger, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public FileAppLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, $"secret-base-{DateTime.UtcNow:yyyyMMdd}.log");
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public void Info(string category, string message) => Write("INFO", category, message, null);

    public void Warn(string category, string message) => Write("WARN", category, message, null);

    public void Error(string category, string message, Exception? exception = null) =>
        Write("ERROR", category, message, exception);

    private void Write(string level, string category, string message, Exception? exception)
    {
        var line = $"{DateTime.UtcNow:O} [{level}] [{category}] {message}";
        if (exception is not null)
        {
            line += $" | {exception.GetType().Name}: {exception.Message}";
        }

        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose() => _writer.Dispose();
}
