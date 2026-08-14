namespace SecretBase.Core.Creative;

/// <summary>
/// A creative activity container — not a filesystem delete target.
/// Delete Project removes this registration only.
/// </summary>
public sealed class CreativeProject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public CreativeProjectType ProjectType { get; set; } = CreativeProjectType.Other;

    /// <summary>Optional absolute root folder path.</summary>
    public string? RootFolder { get; set; }

    public bool IsFavorite { get; set; }

    public DateTimeOffset DateAdded { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastOpened { get; set; }

    public List<CreativeProjectResource> Resources { get; set; } = [];
}
