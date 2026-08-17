namespace SecretBase.Core.Apps;

/// <summary>In-memory custom app store for tests.</summary>
public sealed class MemoryCustomAppStore : ICustomAppStore
{
    private CustomAppDocument _document = new();

    public CustomAppDocument LoadOrCreate() =>
        CustomAppDocumentMigrator.MigrateToCurrent(new CustomAppDocument
        {
            SchemaVersion = _document.SchemaVersion,
            Apps = _document.Apps.Select(Clone).ToList()
        });

    public void Save(CustomAppDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var migrated = CustomAppDocumentMigrator.MigrateToCurrent(document);
        _document = new CustomAppDocument
        {
            SchemaVersion = CustomAppDocument.CurrentSchemaVersion,
            Apps = migrated.Apps.Select(Clone).ToList()
        };
    }

    private static CustomApp Clone(CustomApp app) =>
        new()
        {
            Id = app.Id,
            Name = app.Name,
            Description = app.Description,
            LaunchTarget = app.LaunchTarget,
            Type = app.Type,
            ProjectRoot = app.ProjectRoot,
            CreativeProjectId = app.CreativeProjectId,
            DateAdded = app.DateAdded
        };
}
