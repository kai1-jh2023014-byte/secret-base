namespace SecretBase.Core.Creative;

/// <summary>Allowed Creative Workspace operations for UI and future AI.</summary>
public enum CreativeCommandKind
{
    SearchItems = 0,
    OpenItem = 1,
    OpenFolder = 2,
    OpenProject = 3,
    ToggleFavorite = 4
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
}
