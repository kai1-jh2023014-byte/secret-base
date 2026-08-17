using SecretBase.Core.Apps;
using SecretBase.Core.Calendar;
using SecretBase.Core.Classroom;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Integration;

/// <summary>Future-AI request against the Integration Catalog. No free-form shell.</summary>
public sealed class IntegrationRequest
{
    public required string CommandId { get; init; }

    public string? AppId { get; init; }

    public string? ProjectId { get; init; }

    public string? Query { get; init; }
}

/// <summary>Unified result for catalog execution. Host still owns OS launch.</summary>
public sealed class IntegrationCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public string CommandId { get; init; } = string.Empty;

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public bool ShouldOpenCursorAtFolder { get; init; }

    public string? CursorFolderPath { get; init; }

    public IReadOnlyList<CalendarEvent> Events { get; init; } = Array.Empty<CalendarEvent>();

    public IReadOnlyList<CustomApp> Apps { get; init; } = Array.Empty<CustomApp>();

    public IReadOnlyList<CreativeProject> Projects { get; init; } = Array.Empty<CreativeProject>();

    public static IntegrationCommandResult Ok(
        string commandId,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false,
        bool shouldOpenCursorAtFolder = false,
        string? cursorFolderPath = null,
        IReadOnlyList<CalendarEvent>? events = null,
        IReadOnlyList<CustomApp>? apps = null,
        IReadOnlyList<CreativeProject>? projects = null) =>
        new()
        {
            Succeeded = true,
            CommandId = commandId,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            ShouldOpenCursorAtFolder = shouldOpenCursorAtFolder,
            CursorFolderPath = cursorFolderPath,
            Events = events ?? Array.Empty<CalendarEvent>(),
            Apps = apps ?? Array.Empty<CustomApp>(),
            Projects = projects ?? Array.Empty<CreativeProject>()
        };

    public static IntegrationCommandResult Fail(string commandId, string error) =>
        new()
        {
            Succeeded = false,
            CommandId = commandId,
            ErrorMessage = error
        };
}
