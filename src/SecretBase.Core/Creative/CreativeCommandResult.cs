namespace SecretBase.Core.Creative;

/// <summary>Result of a CreativeCommand. Open does not launch — App/Platform does after validation.</summary>
public sealed class CreativeCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public CreativeCommandKind Kind { get; init; }

    public IReadOnlyList<CreativeItem> Items { get; init; } = Array.Empty<CreativeItem>();

    public CreativeItem? Item { get; init; }

    public IReadOnlyList<CreativeProject> Projects { get; init; } = Array.Empty<CreativeProject>();

    public CreativeProject? Project { get; init; }

    public CreativeProjectResource? Resource { get; init; }

    /// <summary>Absolute path or https URL for host to open when <see cref="ShouldLaunch"/>.</summary>
    public string? LaunchTarget { get; init; }

    /// <summary>When true, host may open <see cref="LaunchTarget"/> (path via launcher, https via browser).</summary>
    public bool ShouldLaunch { get; init; }

    /// <summary>True when LaunchTarget is an external https link.</summary>
    public bool LaunchIsExternalLink { get; init; }

    public static CreativeCommandResult Ok(
        CreativeCommandKind kind,
        IReadOnlyList<CreativeItem>? items = null,
        CreativeItem? item = null,
        bool shouldLaunch = false,
        IReadOnlyList<CreativeProject>? projects = null,
        CreativeProject? project = null,
        CreativeProjectResource? resource = null,
        string? launchTarget = null,
        bool launchIsExternalLink = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Items = items ?? Array.Empty<CreativeItem>(),
            Item = item,
            Projects = projects ?? Array.Empty<CreativeProject>(),
            Project = project,
            Resource = resource,
            LaunchTarget = launchTarget,
            ShouldLaunch = shouldLaunch,
            LaunchIsExternalLink = launchIsExternalLink
        };

    public static CreativeCommandResult Fail(CreativeCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
