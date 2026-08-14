namespace SecretBase.Core.Creative;

/// <summary>In-memory store for tests (no disk).</summary>
public sealed class MemoryCreativeWorkspaceStore : ICreativeWorkspaceStore
{
    private CreativeWorkspaceDocument _document = new();

    public CreativeWorkspaceDocument LoadOrCreate() =>
        new()
        {
            SchemaVersion = _document.SchemaVersion,
            Items = _document.Items.Select(Clone).ToList()
        };

    public void Save(CreativeWorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = new CreativeWorkspaceDocument
        {
            SchemaVersion = CreativeWorkspaceDocument.CurrentSchemaVersion,
            Items = document.Items.Select(Clone).ToList()
        };
    }

    private static CreativeItem Clone(CreativeItem i) =>
        new()
        {
            Id = i.Id,
            Name = i.Name,
            Path = i.Path,
            ItemType = i.ItemType,
            IsFavorite = i.IsFavorite,
            DateAdded = i.DateAdded,
            LastOpened = i.LastOpened,
            IconHint = i.IconHint
        };
}
