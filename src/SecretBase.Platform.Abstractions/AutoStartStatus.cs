namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Describes whether Secret Base is registered for Windows logon startup.
/// </summary>
public sealed class AutoStartStatus
{
    public required bool IsRegistered { get; init; }

    /// <summary>Executable path stored in the Windows startup entry, if any.</summary>
    public string? RegisteredCommand { get; init; }

    /// <summary>True when the registered command targets the current process executable.</summary>
    public bool PointsToCurrentExecutable { get; init; }

    public string? ErrorMessage { get; init; }
}
