namespace SecretBase.Core.Creative;

/// <summary>
/// Snapshot of something opened from a Project Dashboard (Secret Base only — no FS watcher).
/// </summary>
public sealed class CreativeProjectRecentItem
{
    public const string RootKey = "__root__";

    /// <summary>Resource id, or <see cref="RootKey"/> for the project root folder.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CreativeProjectResourceKind Kind { get; set; } = CreativeProjectResourceKind.File;

    public string Target { get; set; } = string.Empty;

    public bool IsRoot { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
}
