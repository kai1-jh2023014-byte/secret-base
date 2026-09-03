namespace SecretBase.Core.Workspace;

/// <summary>
/// Prepared work mode for the Base. Does not launch apps or open files by itself.
/// </summary>
public sealed class WorkspaceSession
{
    public string Title { get; init; } = "Workspace";

    public string? ProjectId { get; init; }

    public string? ProjectName { get; init; }

    public string LastSessionSummary { get; init; } = string.Empty;

    public List<string> SuggestedAppNames { get; set; } = [];

    public List<string> SuggestedFileNames { get; set; } = [];

    public List<string> PreparedChecks { get; set; } = [];

    public string NextTask { get; init; } = string.Empty;

    public DateTimeOffset PreparedAt { get; init; }

    public bool FocusWasRunning { get; init; }

    public string AiNote { get; init; } = string.Empty;

    public string StatusLine =>
        string.IsNullOrWhiteSpace(ProjectName) ? Title : $"{Title} · {ProjectName}";
}
