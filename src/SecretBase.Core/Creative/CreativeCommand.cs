namespace SecretBase.Core.Creative;

/// <summary>Allowed Creative Workspace operations for UI and future AI.</summary>
public enum CreativeCommandKind
{
    SearchItems = 0,
    OpenItem = 1,
    OpenFolder = 2,
    /// <summary>Legacy: open a CreativeItem marked as Project (folder registration).</summary>
    OpenProject = 3,
    ToggleFavorite = 4,

    SearchProjects = 10,
    OpenCreativeProject = 11,
    OpenCreativeProjectRoot = 12,
    OpenCreativeProjectResource = 13,
    ToggleCreativeProjectFavorite = 14,
    DeleteCreativeProjectRegistration = 15
}

/// <summary>
/// Validated creative intent. Future AI must emit these — never pick arbitrary OS paths
/// or run shell commands.
/// </summary>
public sealed class CreativeCommand
{
    public CreativeCommandKind Kind { get; init; }

    public string? Query { get; init; }

    public string? ItemId { get; init; }

    public string? ProjectId { get; init; }

    public string? ResourceId { get; init; }

    public static CreativeCommand SearchItems(string? query) =>
        new() { Kind = CreativeCommandKind.SearchItems, Query = query };

    public static CreativeCommand OpenItem(string itemId) =>
        new() { Kind = CreativeCommandKind.OpenItem, ItemId = itemId };

    public static CreativeCommand OpenFolder(string itemId) =>
        new() { Kind = CreativeCommandKind.OpenFolder, ItemId = itemId };

    public static CreativeCommand OpenProject(string itemId) =>
        new() { Kind = CreativeCommandKind.OpenProject, ItemId = itemId };

    public static CreativeCommand ToggleFavorite(string itemId) =>
        new() { Kind = CreativeCommandKind.ToggleFavorite, ItemId = itemId };

    public static CreativeCommand SearchProjects(string? query) =>
        new() { Kind = CreativeCommandKind.SearchProjects, Query = query };

    public static CreativeCommand OpenCreativeProject(string projectId) =>
        new() { Kind = CreativeCommandKind.OpenCreativeProject, ProjectId = projectId };

    public static CreativeCommand OpenCreativeProjectRoot(string projectId) =>
        new() { Kind = CreativeCommandKind.OpenCreativeProjectRoot, ProjectId = projectId };

    public static CreativeCommand OpenCreativeProjectResource(string projectId, string resourceId) =>
        new()
        {
            Kind = CreativeCommandKind.OpenCreativeProjectResource,
            ProjectId = projectId,
            ResourceId = resourceId
        };

    public static CreativeCommand ToggleCreativeProjectFavorite(string projectId) =>
        new() { Kind = CreativeCommandKind.ToggleCreativeProjectFavorite, ProjectId = projectId };

    /// <summary>Removes Secret Base registration only — never deletes OS files.</summary>
    public static CreativeCommand DeleteCreativeProjectRegistration(string projectId) =>
        new() { Kind = CreativeCommandKind.DeleteCreativeProjectRegistration, ProjectId = projectId };
}
