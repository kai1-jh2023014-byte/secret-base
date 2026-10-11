using System.Text;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Assistant;

/// <summary>
/// Sandboxed file access under a registered Creative Project <see cref="CreativeProject.RootFolder"/>.
/// No delete, no absolute paths from the model, no symlink escape, bounded size.
/// </summary>
public static class ProjectFileAccess
{
    public const int MaxReadBytes = 64 * 1024;
    public const int MaxWriteBytes = 64 * 1024;
    public const int MaxListEntries = 100;
    public const int MaxListDepth = 2;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".cs", ".json", ".csv", ".xml", ".yml", ".yaml",
        ".gitignore", ".editorconfig", ".props", ".targets", ".razor", ".cshtml"
    };

    public static bool TryResolveProject(
        IReadOnlyList<CreativeProject> projects,
        string? projectId,
        out CreativeProject? project,
        out string error)
    {
        project = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(projectId))
        {
            error = "Missing project_id.";
            return false;
        }

        project = projects.FirstOrDefault(p =>
            string.Equals(p.Id, projectId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (project is null)
        {
            error = "Unknown project_id. Use creative_list_projects first.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(project.RootFolder))
        {
            error = "That project has no registered root folder.";
            project = null;
            return false;
        }

        return true;
    }

    public static bool TryResolveUnderRoot(
        string rootFolder,
        string? relativePath,
        out string fullPath,
        out string error)
    {
        fullPath = string.Empty;
        error = string.Empty;
        var relative = (relativePath ?? string.Empty).Trim().Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(relative) || relative is "." or "./")
        {
            relative = string.Empty;
        }

        if (relative.StartsWith('/')
            || relative.Contains("://", StringComparison.Ordinal)
            || relative.Contains("..", StringComparison.Ordinal)
            || (relative.Length >= 2 && relative[1] == ':'))
        {
            error = "Use a relative path under the project root (no .., no absolute paths).";
            return false;
        }

        if (!CreativePathValidator.TryNormalize(rootFolder, CreativeItemType.Folder, out var root, out var rootError)
            || string.IsNullOrWhiteSpace(root))
        {
            error = rootError ?? "Project root is invalid.";
            return false;
        }

        string candidate;
        try
        {
            candidate = string.IsNullOrEmpty(relative)
                ? Path.GetFullPath(root)
                : Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            root = Path.GetFullPath(root);
        }
        catch (Exception)
        {
            error = "Could not resolve that path.";
            return false;
        }

        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        if (!string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            error = "Path escapes the project root.";
            return false;
        }

        // Symlink / reparse escape: require the resolved path to still live under root.
        try
        {
            if (Directory.Exists(candidate) || File.Exists(candidate))
            {
                var attr = File.GetAttributes(candidate);
                if (attr.HasFlag(FileAttributes.ReparsePoint))
                {
                    error = "Links outside the project root are not allowed.";
                    return false;
                }
            }
        }
        catch
        {
            // Existence probe is best-effort.
        }

        fullPath = candidate;
        return true;
    }

    public static bool IsAllowedFileExtension(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        if (string.Equals(name, ".gitignore", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, ".editorconfig", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fullPath);
        return !string.IsNullOrEmpty(ext) && AllowedExtensions.Contains(ext);
    }

    public static string ListRelative(
        string rootFolder,
        string? relativeDirectory,
        out string error)
    {
        error = string.Empty;
        if (!TryResolveUnderRoot(rootFolder, relativeDirectory, out var dir, out error))
        {
            return string.Empty;
        }

        if (!Directory.Exists(dir))
        {
            error = "Directory not found under the project root.";
            return string.Empty;
        }

        var rootFull = Path.GetFullPath(rootFolder);
        var lines = new List<string>();
        void Walk(string current, int depth)
        {
            if (lines.Count >= MaxListEntries || depth > MaxListDepth)
            {
                return;
            }

            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(current).OrderBy(p => p, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return;
            }

            foreach (var entry in entries)
            {
                if (lines.Count >= MaxListEntries)
                {
                    break;
                }

                string rel;
                try
                {
                    rel = Path.GetRelativePath(rootFull, entry).Replace('\\', '/');
                }
                catch
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    lines.Add(rel.TrimEnd('/') + "/");
                    if (depth < MaxListDepth)
                    {
                        Walk(entry, depth + 1);
                    }
                }
                else
                {
                    lines.Add(rel);
                }
            }
        }

        Walk(dir, 0);
        if (lines.Count == 0)
        {
            return "(empty)";
        }

        var body = string.Join('\n', lines);
        if (lines.Count >= MaxListEntries)
        {
            body += "\n… truncated …";
        }

        return body;
    }

    public static string ReadText(string fullPath, out string error)
    {
        error = string.Empty;
        if (!File.Exists(fullPath))
        {
            error = "File not found under the project root.";
            return string.Empty;
        }

        if (!IsAllowedFileExtension(fullPath))
        {
            error = "That file type is not allowed for assistant read.";
            return string.Empty;
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (info.Length > MaxReadBytes)
            {
                error = $"File is larger than {MaxReadBytes} bytes.";
                return string.Empty;
            }

            return File.ReadAllText(fullPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            error = "Could not read file: " + ex.Message;
            return string.Empty;
        }
    }

    public static bool WriteText(string fullPath, string content, out string error, out string? previous)
    {
        error = string.Empty;
        previous = null;
        if (!IsAllowedFileExtension(fullPath))
        {
            error = "That file type is not allowed for assistant write.";
            return false;
        }

        var bytes = Encoding.UTF8.GetByteCount(content ?? string.Empty);
        if (bytes > MaxWriteBytes)
        {
            error = $"Content is larger than {MaxWriteBytes} bytes.";
            return false;
        }

        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                error = "Parent directory must already exist under the project root.";
                return false;
            }

            if (File.Exists(fullPath))
            {
                var existing = new FileInfo(fullPath);
                if (existing.Length <= MaxReadBytes)
                {
                    previous = File.ReadAllText(fullPath, Encoding.UTF8);
                }
            }

            File.WriteAllText(fullPath, content ?? string.Empty, Encoding.UTF8);
            return true;
        }
        catch (Exception ex)
        {
            error = "Could not write file: " + ex.Message;
            return false;
        }
    }
}
