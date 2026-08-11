namespace SecretBase.Core.Time;

/// <summary>
/// Abstraction over "now" so clock logic and tests are not tied to wall-clock time.
/// </summary>
public interface ITimeProvider
{
    DateTimeOffset GetLocalNow();
}
