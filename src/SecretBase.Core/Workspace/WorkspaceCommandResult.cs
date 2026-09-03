namespace SecretBase.Core.Workspace;

public sealed class WorkspaceCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public WorkspaceCommandKind Kind { get; init; }

    public string? Message { get; init; }

    public WorkspaceNamedEntry? Match { get; init; }

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public static WorkspaceCommandResult Ok(
        WorkspaceCommandKind kind,
        string message,
        WorkspaceNamedEntry? match = null,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Message = message,
            Match = match,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink
        };

    public static WorkspaceCommandResult Fail(WorkspaceCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error,
            Message = error
        };
}
