namespace SecretBase.Core.Creative;

/// <summary>
/// A user-registered file, folder, or project reference.
/// Stores a path reference only — never credentials or free-form commands.
/// </summary>
public sealed class CreativeItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>Absolute Windows path chosen by the user.</summary>
    public string Path { get; set; } = string.Empty;

    public CreativeItemType ItemType { get; set; } = CreativeItemType.File;

    public bool IsFavorite { get; set; }

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastOpened { get; set; }

    /// <summary>Optional UI hint (e.g. extension). Not an executable command.</summary>
    public string? IconHint { get; set; }
}
