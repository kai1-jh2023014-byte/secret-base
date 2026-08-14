namespace SecretBase.Core.Creative;

/// <summary>Persisted projects document (AppData creative/projects.json).</summary>
public sealed class CreativeProjectDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CreativeProject> Projects { get; set; } = [];
}
