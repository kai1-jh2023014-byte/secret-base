namespace SecretBase.Core.Creative;

/// <summary>
/// CRUD for CreativeProject registrations and Dashboard state.
/// Never deletes/moves OS files. Remove resource = registration only.
/// </summary>
public sealed class CreativeProjectService
{
    public const int MaxRecentItems = 10;

    private readonly ICreativeProjectStore _store;
    private CreativeProjectDocument _document;
    private readonly object _gate = new();

    public CreativeProjectService(ICreativeProjectStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _document = CreativeProjectDocumentMigrator.MigrateToCurrent(_store.LoadOrCreate());
        if (_document.SchemaVersion != CreativeProjectDocument.CurrentSchemaVersion)
        {
            Persist();
        }
    }

    public IReadOnlyList<CreativeProject> Projects
    {
        get
        {
            lock (_gate)
            {
                return _document.Projects.Select(Clone).ToList();
            }
        }
    }

    public IReadOnlyList<CreativeProject> GetFavorites()
    {
        lock (_gate)
        {
            return _document.Projects
                .Where(p => p.IsFavorite)
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public CreativeProject? FindById(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_gate)
        {
            var p = _document.Projects.FirstOrDefault(x =>
                string.Equals(x.Id, id, StringComparison.Ordinal));
            return p is null ? null : Clone(p);
        }
    }

    public IReadOnlyList<CreativeProject> Search(string? query)
    {
        var q = query?.Trim() ?? string.Empty;
        lock (_gate)
        {
            IEnumerable<CreativeProject> source = _document.Projects;
            if (!string.IsNullOrEmpty(q))
            {
                source = source.Where(p =>
                    p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (p.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || p.ProjectType.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (p.RootFolder?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (p.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return source
                .OrderByDescending(p => p.IsFavorite)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(Clone)
                .ToList();
        }
    }

    public bool TryCreate(
        string name,
        string? description,
        CreativeProjectType type,
        string? rootFolder,
        out CreativeProject? project,
        out string? error)
    {
        project = null;
        error = null;
        if (!CreativeProjectValidator.TryValidateName(name, out var normalizedName, out var nameError))
        {
            error = nameError;
            return false;
        }

        if (!CreativeProjectValidator.TryNormalizeRootFolder(rootFolder, out var root, out var rootError))
        {
            error = rootError;
            return false;
        }

        var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (desc is { Length: > CreativeProjectValidator.MaxDescriptionLength })
        {
            error = "Description is too long.";
            return false;
        }

        lock (_gate)
        {
            var created = new CreativeProject
            {
                Name = normalizedName,
                Description = desc,
                ProjectType = type,
                RootFolder = root,
                DateAdded = DateTimeOffset.UtcNow
            };
            _document.Projects.Add(created);
            Persist();
            project = Clone(created);
            return true;
        }
    }

    public bool TryUpdate(
        string id,
        string name,
        string? description,
        CreativeProjectType type,
        string? rootFolder,
        out CreativeProject? project,
        out string? error)
    {
        project = null;
        error = null;
        if (!CreativeProjectValidator.TryValidateName(name, out var normalizedName, out var nameError))
        {
            error = nameError;
            return false;
        }

        if (!CreativeProjectValidator.TryNormalizeRootFolder(rootFolder, out var root, out var rootError))
        {
            error = rootError;
            return false;
        }

        var desc = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (desc is { Length: > CreativeProjectValidator.MaxDescriptionLength })
        {
            error = "Description is too long.";
            return false;
        }

        lock (_gate)
        {
            var existing = FindMutable(id);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            existing.Name = normalizedName;
            existing.Description = desc;
            existing.ProjectType = type;
            existing.RootFolder = root;
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    /// <summary>Removes project registration only — never deletes OS files/folders.</summary>
    public bool TryDeleteRegistration(string id, out string? error)
    {
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(id);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            _document.Projects.Remove(existing);
            Persist();
            return true;
        }
    }

    public bool TryToggleFavorite(string id, out CreativeProject? project, out string? error)
    {
        project = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(id);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            existing.IsFavorite = !existing.IsFavorite;
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    public bool TrySaveNotes(string id, string? notes, out CreativeProject? project, out string? error)
    {
        project = null;
        error = null;
        if (!CreativeProjectValidator.TryNormalizeNotes(notes, out var normalized, out var notesError))
        {
            error = notesError;
            return false;
        }

        lock (_gate)
        {
            var existing = FindMutable(id);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            existing.Notes = normalized;
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    public bool TryToggleResourceQuickAction(
        string projectId,
        string resourceId,
        out CreativeProject? project,
        out string? error)
    {
        project = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            var resource = existing.Resources.FirstOrDefault(r =>
                string.Equals(r.Id, resourceId, StringComparison.Ordinal));
            if (resource is null)
            {
                error = "Resource not found.";
                return false;
            }

            resource.IsQuickAction = !resource.IsQuickAction;
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    public IReadOnlyList<CreativeProjectResource> GetQuickActions(string? projectId)
    {
        var project = FindById(projectId);
        if (project is null)
        {
            return Array.Empty<CreativeProjectResource>();
        }

        return project.Resources
            .Where(r => r.IsQuickAction)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool TryMarkOpened(string id, DateTimeOffset openedAt, out CreativeProject? project, out string? error)
    {
        project = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(id);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            existing.LastOpened = openedAt;
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    /// <summary>Records root folder open in Recent (Secret Base open only).</summary>
    public bool TryRecordRootOpened(
        string projectId,
        DateTimeOffset openedAt,
        out CreativeProject? project,
        out string? error)
    {
        project = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(existing.RootFolder))
            {
                error = "Project has no root folder.";
                return false;
            }

            existing.LastOpened = openedAt;
            PushRecent(existing, new CreativeProjectRecentItem
            {
                Key = CreativeProjectRecentItem.RootKey,
                Name = "Root Folder",
                Kind = CreativeProjectResourceKind.Folder,
                Target = existing.RootFolder!,
                IsRoot = true,
                OpenedAt = openedAt
            });
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    /// <summary>Records resource open in Recent (Secret Base open only).</summary>
    public bool TryRecordResourceOpened(
        string projectId,
        string resourceId,
        DateTimeOffset openedAt,
        out CreativeProject? project,
        out CreativeProjectResource? resource,
        out string? error)
    {
        project = null;
        resource = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            var found = existing.Resources.FirstOrDefault(r =>
                string.Equals(r.Id, resourceId, StringComparison.Ordinal));
            if (found is null)
            {
                error = "Resource not found.";
                return false;
            }

            existing.LastOpened = openedAt;
            PushRecent(existing, new CreativeProjectRecentItem
            {
                Key = found.Id,
                Name = found.Name,
                Kind = found.Kind,
                Target = found.Target,
                IsRoot = false,
                OpenedAt = openedAt
            });
            Persist();
            project = Clone(existing);
            resource = new CreativeProjectResource
            {
                Id = found.Id,
                Name = found.Name,
                Kind = found.Kind,
                Target = found.Target,
                IsQuickAction = found.IsQuickAction
            };
            return true;
        }
    }

    public bool TryAddResource(
        string projectId,
        CreativeProjectResourceKind kind,
        string name,
        string target,
        out CreativeProject? project,
        out string? error)
    {
        project = null;
        error = null;
        if (!CreativeProjectValidator.TryNormalizeResource(kind, name, target, out var resource, out var resError)
            || resource is null)
        {
            error = resError;
            return false;
        }

        lock (_gate)
        {
            var existing = FindMutable(projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            if (existing.Resources.Any(r =>
                    string.Equals(r.Target, resource.Target, StringComparison.OrdinalIgnoreCase)))
            {
                error = "This resource is already registered.";
                return false;
            }

            existing.Resources.Add(resource);
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    /// <summary>Removes resource registration only — never deletes OS files.</summary>
    public bool TryRemoveResource(string projectId, string resourceId, out CreativeProject? project, out string? error)
    {
        project = null;
        error = null;
        lock (_gate)
        {
            var existing = FindMutable(projectId);
            if (existing is null)
            {
                error = "Project not found.";
                return false;
            }

            var resource = existing.Resources.FirstOrDefault(r =>
                string.Equals(r.Id, resourceId, StringComparison.Ordinal));
            if (resource is null)
            {
                error = "Resource not found.";
                return false;
            }

            existing.Resources.Remove(resource);
            existing.RecentItems.RemoveAll(r =>
                string.Equals(r.Key, resourceId, StringComparison.Ordinal));
            Persist();
            project = Clone(existing);
            return true;
        }
    }

    private CreativeProject? FindMutable(string id) =>
        _document.Projects.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    private static void PushRecent(CreativeProject project, CreativeProjectRecentItem entry)
    {
        project.RecentItems.RemoveAll(r =>
            string.Equals(r.Key, entry.Key, StringComparison.Ordinal));
        project.RecentItems.Insert(0, entry);
        if (project.RecentItems.Count > MaxRecentItems)
        {
            project.RecentItems.RemoveRange(MaxRecentItems, project.RecentItems.Count - MaxRecentItems);
        }
    }

    private void Persist()
    {
        _document.SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion;
        _store.Save(_document);
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
            Resources = p.Resources.Select(CloneResource).ToList(),
            RecentItems = p.RecentItems.Select(CloneRecent).ToList()
        };

    private static CreativeProjectResource CloneResource(CreativeProjectResource r) =>
        new()
        {
            Id = r.Id,
            Name = r.Name,
            Kind = r.Kind,
            Target = r.Target,
            IsQuickAction = r.IsQuickAction
        };

    private static CreativeProjectRecentItem CloneRecent(CreativeProjectRecentItem r) =>
        new()
        {
            Key = r.Key,
            Name = r.Name,
            Kind = r.Kind,
            Target = r.Target,
            IsRoot = r.IsRoot,
            OpenedAt = r.OpenedAt
        };
}
