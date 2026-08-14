namespace SecretBase.Core.Creative;

/// <summary>
/// In-memory workspace operations over a persisted document.
/// Does not launch processes, delete files, or run shell commands.
/// </summary>
public sealed class CreativeWorkspaceService
{
    public const int MaxRecent = 10;

    private readonly ICreativeWorkspaceStore _store;
    private CreativeWorkspaceDocument _document;
    private readonly object _gate = new();

    public CreativeWorkspaceService(ICreativeWorkspaceStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _document = _store.LoadOrCreate();
        if (_document.SchemaVersion < 1)
        {
            _document.SchemaVersion = CreativeWorkspaceDocument.CurrentSchemaVersion;
        }
    }

    public IReadOnlyList<CreativeItem> Items
    {
        get
        {
            lock (_gate)
            {
                return _document.Items.Select(Clone).ToList();
            }
        }
    }

    public IReadOnlyList<CreativeItem> GetFavorites()
    {
        lock (_gate)
        {
            return _document.Items
                .Where(i => i.IsFavorite)
                .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<CreativeItem> GetRecent()
    {
        lock (_gate)
        {
            return _document.Items
                .Where(i => i.LastOpened is not null)
                .OrderByDescending(i => i.LastOpened)
                .Take(MaxRecent)
                .Select(Clone)
                .ToList();
        }
    }

    public IReadOnlyList<CreativeItem> Search(string? query)
    {
        var q = query?.Trim() ?? string.Empty;
        lock (_gate)
        {
            IEnumerable<CreativeItem> source = _document.Items;
            if (!string.IsNullOrEmpty(q))
            {
                source = source.Where(i =>
                    i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || i.Path.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || i.ItemType.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            return source
                .OrderByDescending(i => i.IsFavorite)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public CreativeItem? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_gate)
        {
            var item = _document.Items.FirstOrDefault(i =>
                string.Equals(i.Id, id, StringComparison.Ordinal));
            return item is null ? null : Clone(item);
        }
    }

    public bool TryAdd(
        string path,
        CreativeItemType itemType,
        string? displayName,
        out CreativeItem? item,
        out string? error)
    {
        item = null;
        error = null;

        if (!CreativePathValidator.TryNormalize(path, itemType, out var normalized, out var validationError))
        {
            error = validationError;
            return false;
        }

        lock (_gate)
        {
            if (_document.Items.Any(i =>
                    string.Equals(i.Path, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                error = "This path is already registered.";
                return false;
            }

            var created = new CreativeItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(displayName)
                    ? CreativePathValidator.InferName(normalized)
                    : displayName.Trim(),
                Path = normalized,
                ItemType = itemType,
                IsFavorite = false,
                DateAdded = DateTimeOffset.UtcNow,
                IconHint = CreativePathValidator.InferIconHint(normalized, itemType)
            };

            _document.Items.Add(created);
            Persist();
            item = Clone(created);
            return true;
        }
    }

    public bool TrySetFavorite(string id, bool isFavorite, out string? error)
    {
        error = null;
        lock (_gate)
        {
            var item = _document.Items.FirstOrDefault(i =>
                string.Equals(i.Id, id, StringComparison.Ordinal));
            if (item is null)
            {
                error = "Item not found.";
                return false;
            }

            item.IsFavorite = isFavorite;
            Persist();
            return true;
        }
    }

    public bool TryToggleFavorite(string id, out CreativeItem? item, out string? error)
    {
        item = null;
        error = null;
        lock (_gate)
        {
            var existing = _document.Items.FirstOrDefault(i =>
                string.Equals(i.Id, id, StringComparison.Ordinal));
            if (existing is null)
            {
                error = "Item not found.";
                return false;
            }

            existing.IsFavorite = !existing.IsFavorite;
            Persist();
            item = Clone(existing);
            return true;
        }
    }

    /// <summary>Records LastOpened for a registered item. Does not launch anything.</summary>
    public bool TryMarkOpened(string id, DateTimeOffset openedAt, out CreativeItem? item, out string? error)
    {
        item = null;
        error = null;
        lock (_gate)
        {
            var existing = _document.Items.FirstOrDefault(i =>
                string.Equals(i.Id, id, StringComparison.Ordinal));
            if (existing is null)
            {
                error = "Item not found.";
                return false;
            }

            existing.LastOpened = openedAt;
            Persist();
            item = Clone(existing);
            return true;
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _document = _store.LoadOrCreate();
        }
    }

    private void Persist()
    {
        _document.SchemaVersion = CreativeWorkspaceDocument.CurrentSchemaVersion;
        _store.Save(_document);
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
