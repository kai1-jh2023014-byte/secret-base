using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Creative;

/// <summary>Validates project fields and resources (no OS launch / delete).</summary>
public static class CreativeProjectValidator
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 1000;
    public const int MaxNotesLength = 4000;

    public static bool TryValidateName(string? name, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Project name is required.";
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            error = "Project name is too long.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    public static bool TryNormalizeRootFolder(string? path, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        if (!CreativePathValidator.TryNormalize(path, CreativeItemType.Folder, out var folder, out var err))
        {
            error = err;
            return false;
        }

        normalized = folder;
        return true;
    }

    public static bool TryNormalizeResource(
        CreativeProjectResourceKind kind,
        string? name,
        string? target,
        out CreativeProjectResource? resource,
        out string error)
    {
        resource = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Resource name is required.";
            return false;
        }

        if (kind == CreativeProjectResourceKind.ExternalLink)
        {
            if (!WebUrlValidator.TryNormalize(target, out var url, out var urlError))
            {
                error = urlError ?? WebUrlValidator.BlockedMessage;
                return false;
            }

            resource = new CreativeProjectResource
            {
                Name = name.Trim(),
                Kind = kind,
                Target = url!
            };
            return true;
        }

        var itemType = kind == CreativeProjectResourceKind.Folder
            ? CreativeItemType.Folder
            : CreativeItemType.File;
        if (!CreativePathValidator.TryNormalize(target, itemType, out var path, out var pathError))
        {
            error = pathError;
            return false;
        }

        resource = new CreativeProjectResource
        {
            Name = name.Trim(),
            Kind = kind,
            Target = path
        };
        return true;
    }

    public static bool TryNormalizeNotes(string? notes, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrEmpty(notes))
        {
            return true;
        }

        // Preserve intentional whitespace; only reject oversized payloads.
        if (notes.Length > MaxNotesLength)
        {
            error = "Notes are too long.";
            return false;
        }

        normalized = notes;
        return true;
    }
}
