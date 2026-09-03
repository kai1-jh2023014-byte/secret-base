using SecretBase.Core.Blocks;

namespace SecretBase.Core.Creative;

/// <summary>
/// Validates Creative Workspace paths (absolute, no command lines).
/// Reuses Block path rules; does not delete/move/rename.
/// </summary>
public static class CreativePathValidator
{
    public static bool TryNormalize(
        string? path,
        CreativeItemType itemType,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        var blockType = itemType switch
        {
            CreativeItemType.Folder or CreativeItemType.Project => BlockItemType.Folder,
            _ => BlockItemType.File
        };

        if (!BlockTargetValidator.TryValidate(path, blockType, out normalized, out error))
        {
            return false;
        }

        // Soft-normalize separators for stable duplicate detection (Windows vs POSIX).
        normalized = HostPath.NormalizeSeparators(normalized);
        if (HostPath.IsPosixAbsolute(normalized))
        {
            normalized = normalized.TrimEnd('/');
            if (normalized.Length == 0)
            {
                normalized = "/";
            }
        }
        else
        {
            normalized = normalized.TrimEnd('\\');
            if (normalized.Length == 2 && normalized[1] == ':')
            {
                // Drive root — keep trailing slash semantically as "C:"
                normalized += "\\";
            }
        }

        return true;
    }

    public static string InferName(string absolutePath)
    {
        var name = BlockTargetValidator.InferDisplayName(absolutePath);
        return string.IsNullOrWhiteSpace(name) ? "Item" : name;
    }

    public static string? InferIconHint(string absolutePath, CreativeItemType type)
    {
        if (type is CreativeItemType.Folder or CreativeItemType.Project)
        {
            return "folder";
        }

        var ext = HostPath.GetExtension(absolutePath);
        if (string.IsNullOrEmpty(ext) || ext.Length == 1)
        {
            return "file";
        }

        return ext.TrimStart('.').ToLowerInvariant();
    }
}
