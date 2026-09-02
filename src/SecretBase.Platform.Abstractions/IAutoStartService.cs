namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Registers Secret Base for per-user Windows logon startup (HKCU Run).
/// Implementations must not require elevation or modify machine-wide shell state.
/// </summary>
public interface IAutoStartService
{
    /// <summary>True when startup registration is supported on this OS/build.</summary>
    bool IsSupported { get; }

    /// <summary>Reads the current Windows startup registration state.</summary>
    AutoStartStatus GetStatus();

    /// <summary>
    /// Registers the current application executable for logon startup.
    /// Fails when running under <c>dotnet run</c> (no stable app exe path).
    /// </summary>
    bool TryEnable(out string? errorMessage);

    /// <summary>Removes the Windows startup registration entry.</summary>
    bool TryDisable(out string? errorMessage);

    /// <summary>Resolves the executable path used for startup registration.</summary>
    bool TryGetStartupExecutablePath(out string executablePath, out string? errorMessage);
}
