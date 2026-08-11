namespace SecretBase.Infrastructure.Logging;

/// <summary>
/// Minimal structured logging contract. Implementations must avoid writing secrets / PII.
/// </summary>
public interface IAppLogger
{
    void Info(string category, string message);
    void Warn(string category, string message);
    void Error(string category, string message, Exception? exception = null);
}
