namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Describes whether Secret Base is registered for per-user login startup.
/// </summary>
public sealed class AutoStartStatus
{
    public required bool IsRegistered { get; init; }

    /// <summary>Executable path stored in the login auto-start entry, if any.</summary>
    public string? RegisteredCommand { get; init; }

    /// <summary>True when the registered command targets the current process executable.</summary>
    public bool PointsToCurrentExecutable { get; init; }

    public string? ErrorMessage { get; init; }
}
