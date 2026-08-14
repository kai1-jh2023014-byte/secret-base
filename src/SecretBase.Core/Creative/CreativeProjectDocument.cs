namespace SecretBase.Core.Creative;

/// <summary>Persisted projects document (AppData creative/projects.json).</summary>
public sealed class CreativeProjectDocument
{
    /// <summary>v1 = Project CRUD; v2 = Dashboard (notes, quick actions, recent).</summary>
    public const int CurrentSchemaVersion = 2;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CreativeProject> Projects { get; set; } = [];
}
