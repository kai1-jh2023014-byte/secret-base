namespace SecretBase.Core.Apps;

/// <summary>Persisted custom app registry. schemaVersion 1.</summary>
public sealed class CustomAppDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CustomApp> Apps { get; set; } = [];
}

/// <summary>Bumps unknown/old schema to current. No field rewrite in v1.</summary>
public static class CustomAppDocumentMigrator
{
    public static CustomAppDocument MigrateToCurrent(CustomAppDocument? document)
    {
        var doc = document ?? new CustomAppDocument();
        doc.Apps ??= [];
        if (doc.SchemaVersion < 1)
        {
            doc.SchemaVersion = CustomAppDocument.CurrentSchemaVersion;
        }

        if (doc.SchemaVersion > CustomAppDocument.CurrentSchemaVersion)
        {
            doc.SchemaVersion = CustomAppDocument.CurrentSchemaVersion;
        }

        return doc;
    }
}

public interface ICustomAppStore
{
    CustomAppDocument LoadOrCreate();

    void Save(CustomAppDocument document);
}
