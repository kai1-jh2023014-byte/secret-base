namespace SecretBase.Core.Creative;

/// <summary>
/// A registered file, folder, or https link inside a project.
/// Paths/URLs only — never credentials or free-form commands.
/// </summary>
public sealed class CreativeProjectResource
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public CreativeProjectResourceKind Kind { get; set; } = CreativeProjectResourceKind.File;

    /// <summary>Absolute path (File/Folder) or http(s) URL (ExternalLink).</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>When true, shown in Dashboard Quick Actions (user-explicit).</summary>
    public bool IsQuickAction { get; set; }
}
