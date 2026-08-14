namespace SecretBase.Core.Creative;

/// <summary>In-memory project store for tests.</summary>
public sealed class MemoryCreativeProjectStore : ICreativeProjectStore
{
    private CreativeProjectDocument _document = new();

    public CreativeProjectDocument LoadOrCreate() =>
        CreativeProjectDocumentMigrator.MigrateToCurrent(new CreativeProjectDocument
        {
            SchemaVersion = _document.SchemaVersion,
            Projects = _document.Projects.Select(Clone).ToList()
        });

    public void Save(CreativeProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var migrated = CreativeProjectDocumentMigrator.MigrateToCurrent(document);
        _document = new CreativeProjectDocument
        {
            SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion,
            Projects = migrated.Projects.Select(Clone).ToList()
        };
    }

    private static CreativeProject Clone(CreativeProject p) =>
        new()
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            ProjectType = p.ProjectType,
            RootFolder = p.RootFolder,
            IsFavorite = p.IsFavorite,
            Notes = p.Notes,
            DateAdded = p.DateAdded,
            LastOpened = p.LastOpened,
            Resources = p.Resources.Select(r => new CreativeProjectResource
            {
                Id = r.Id,
                Name = r.Name,
                Kind = r.Kind,
                Target = r.Target,
                IsQuickAction = r.IsQuickAction
            }).ToList(),
            RecentItems = p.RecentItems.Select(r => new CreativeProjectRecentItem
            {
                Key = r.Key,
                Name = r.Name,
                Kind = r.Kind,
                Target = r.Target,
                IsRoot = r.IsRoot,
                OpenedAt = r.OpenedAt
            }).ToList()
        };
}
