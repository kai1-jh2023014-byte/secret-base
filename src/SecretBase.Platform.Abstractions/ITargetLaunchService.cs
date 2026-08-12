namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Opens a user-chosen absolute path (application, shortcut, file, or folder)
/// using documented OS launch APIs only. Never runs free-form shell commands.
/// </summary>
public interface ITargetLaunchService
{
    TargetLaunchResult TryLaunch(TargetLaunchRequest request);
}

public sealed record TargetLaunchRequest(
    string Target,
    string ItemType,
    string? DisplayName);

public sealed record TargetLaunchResult(
    bool Succeeded,
    string? ErrorMessage);
