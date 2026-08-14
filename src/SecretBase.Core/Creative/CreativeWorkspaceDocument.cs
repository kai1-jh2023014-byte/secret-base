namespace SecretBase.Core.Creative;

/// <summary>Persisted Creative Workspace document (AppData JSON).</summary>
public sealed class CreativeWorkspaceDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CreativeItem> Items { get; set; } = [];
}
