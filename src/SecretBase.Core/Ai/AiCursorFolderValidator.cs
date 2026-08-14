using SecretBase.Core.Creative;

namespace SecretBase.Core.Ai;

/// <summary>Validates folder paths for Cursor launch (absolute folder only).</summary>
public static class AiCursorFolderValidator
{
    public static bool TryNormalizeProjectRoot(string? path, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Project root not found.";
            return false;
        }

        if (!CreativePathValidator.TryNormalize(path, CreativeItemType.Folder, out normalized, out error))
        {
            error = string.IsNullOrWhiteSpace(error) ? "Project root not found." : error;
            return false;
        }

        return true;
    }
}
