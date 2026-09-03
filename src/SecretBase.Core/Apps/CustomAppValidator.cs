using SecretBase.Core.Blocks;
using SecretBase.Core.Creative;
using SecretBase.Core.Widgets.Web;

namespace SecretBase.Core.Apps;

/// <summary>Validates custom app registration. No OS existence checks (host reports missing files).</summary>
public static class CustomAppValidator
{
    public const int MaxNameLength = 80;
    public const int MaxDescriptionLength = 280;

    public static bool TryNormalize(
        string? name,
        string? description,
        CustomAppType type,
        string? launchTarget,
        string? projectRoot,
        string? creativeProjectId,
        out CustomApp normalized,
        out string error)
    {
        normalized = new CustomApp();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Name is required.";
            return false;
        }

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            error = "Name is too long.";
            return false;
        }

        string? trimmedDescription = null;
        if (!string.IsNullOrWhiteSpace(description))
        {
            trimmedDescription = description.Trim();
            if (trimmedDescription.Length > MaxDescriptionLength)
            {
                error = "Description is too long.";
                return false;
            }
        }

        string target;
        if (type == CustomAppType.Website)
        {
            if (!WebUrlValidator.TryNormalize(launchTarget, out var url, out var urlError) || url is null)
            {
                error = urlError ?? WebUrlValidator.BlockedMessage;
                return false;
            }

            target = url;
        }
        else
        {
            var blockType = type == CustomAppType.Folder ? BlockItemType.Folder : BlockItemType.Application;
            if (!BlockTargetValidator.TryValidate(launchTarget, blockType, out var path, out var pathError))
            {
                error = pathError;
                return false;
            }

            target = HostPath.NormalizeSeparators(path);
        }

        string? root = null;
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            if (!CreativePathValidator.TryNormalize(
                    projectRoot,
                    CreativeItemType.Folder,
                    out var normalizedRoot,
                    out var rootError))
            {
                error = rootError;
                return false;
            }

            root = normalizedRoot;
        }

        string? projectId = null;
        if (!string.IsNullOrWhiteSpace(creativeProjectId))
        {
            projectId = creativeProjectId.Trim();
            if (projectId.Length > 64)
            {
                error = "Creative project id is too long.";
                return false;
            }
        }

        normalized = new CustomApp
        {
            Name = trimmedName,
            Description = trimmedDescription,
            Type = type,
            LaunchTarget = target,
            ProjectRoot = root,
            CreativeProjectId = projectId
        };
        return true;
    }
}
