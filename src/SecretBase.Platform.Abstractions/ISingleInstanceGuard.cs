namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Ensures only one Secret Base process owns the desktop overlay at a time.
/// Implementations are platform-specific (Windows mutex / macOS file lock).
/// </summary>
public interface ISingleInstanceGuard : IDisposable
{
    /// <summary>
    /// Attempts to acquire the single-instance lock for this process.
    /// Returns false when another instance already holds it.
    /// </summary>
    bool TryAcquire();

    /// <summary>True after a successful <see cref="TryAcquire"/> until <see cref="Dispose"/>.</summary>
    bool IsHeld { get; }
}
