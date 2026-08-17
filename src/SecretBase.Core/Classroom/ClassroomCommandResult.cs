namespace SecretBase.Core.Classroom;

/// <summary>Result of a ClassroomCommand. Host opens URLs after validation.</summary>
public sealed class ClassroomCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public ClassroomCommandKind Kind { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public static ClassroomCommandResult Ok(
        ClassroomCommandKind kind,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink
        };

    public static ClassroomCommandResult Fail(ClassroomCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
