namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Intakes a user-dropped file into a Block. Prefer <b>moving</b> Desktop shortcuts
/// (.lnk/.url) into Secret Base storage so the Desktop original is not duplicated.
/// Program Files executables stay as path references (never relocated).
/// </summary>
public interface IBlockItemIntakeService
{
    BlockItemIntakeResult TryIntake(string sourceAbsolutePath, Guid blockId, Guid itemId);
}

public sealed record BlockItemIntakeResult(
    bool Succeeded,
    string TargetPath,
    bool MovedFromSource,
    string? ErrorMessage);
