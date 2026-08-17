namespace SecretBase.Core.Apps;

/// <summary>Register / list / unregister custom apps. Unregister is Secret Base only — never deletes OS files.</summary>
public sealed class CustomAppService
{
    private readonly ICustomAppStore _store;

    public CustomAppService(ICustomAppStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public IReadOnlyList<CustomApp> List()
    {
        var doc = _store.LoadOrCreate();
        return doc.Apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public bool TryGet(string? id, out CustomApp? app)
    {
        app = null;
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        app = _store.LoadOrCreate().Apps
            .FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        return app is not null;
    }

    public bool TryAdd(CustomApp draft, out CustomApp? saved, out string error)
    {
        saved = null;
        if (!CustomAppValidator.TryNormalize(
                draft.Name,
                draft.Description,
                draft.Type,
                draft.LaunchTarget,
                draft.ProjectRoot,
                draft.CreativeProjectId,
                out var normalized,
                out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(draft.Id))
        {
            normalized.Id = Guid.NewGuid().ToString("N");
        }
        else
        {
            normalized.Id = draft.Id.Trim();
        }

        normalized.DateAdded = draft.DateAdded == default ? DateTimeOffset.UtcNow : draft.DateAdded;

        var doc = _store.LoadOrCreate();
        if (doc.Apps.Any(a => string.Equals(a.Id, normalized.Id, StringComparison.OrdinalIgnoreCase)))
        {
            error = "An app with this id is already registered.";
            return false;
        }

        doc.Apps.Add(normalized);
        _store.Save(doc);
        saved = normalized;
        return true;
    }

    public bool TryRemove(string? id, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "App id is required.";
            return false;
        }

        var doc = _store.LoadOrCreate();
        var removed = doc.Apps.RemoveAll(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            error = "App is not registered.";
            return false;
        }

        _store.Save(doc);
        return true;
    }
}
