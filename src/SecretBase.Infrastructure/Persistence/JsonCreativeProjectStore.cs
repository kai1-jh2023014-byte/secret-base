using System.Text.Json;
using SecretBase.Core.Creative;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>
/// Persists Creative Projects under AppData/creative/projects.json (atomic write).
/// </summary>
public sealed class JsonCreativeProjectStore : ICreativeProjectStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonCreativeProjectStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.CreativeDirectory, "projects.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public CreativeProjectDocument LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = new CreativeProjectDocument();
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<CreativeProjectDocument>(json, _options)
                          ?? new CreativeProjectDocument();
                if (doc.SchemaVersion < 1)
                {
                    doc.SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion;
                }

                doc.Projects ??= [];
                foreach (var project in doc.Projects)
                {
                    project.Resources ??= [];
                }

                // Drop resources with invalid targets; keep projects even if root missing on disk.
                foreach (var project in doc.Projects)
                {
                    project.Resources = project.Resources
                        .Where(r => r is not null
                                    && !string.IsNullOrWhiteSpace(r.Name)
                                    && CreativeProjectValidator.TryNormalizeResource(
                                        r.Kind, r.Name, r.Target, out _, out _))
                        .ToList();

                    if (!string.IsNullOrWhiteSpace(project.RootFolder)
                        && !CreativeProjectValidator.TryNormalizeRootFolder(
                            project.RootFolder, out var normalizedRoot, out _))
                    {
                        project.RootFolder = null;
                    }
                    else if (!string.IsNullOrWhiteSpace(project.RootFolder))
                    {
                        CreativeProjectValidator.TryNormalizeRootFolder(
                            project.RootFolder, out var rooted, out _);
                        project.RootFolder = rooted;
                    }
                }

                doc.Projects = doc.Projects
                    .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Name))
                    .ToList();

                return doc;
            }
            catch (JsonException)
            {
                return new CreativeProjectDocument();
            }
            catch (IOException)
            {
                return new CreativeProjectDocument();
            }
        }
    }

    public void Save(CreativeProjectDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            SaveUnlocked(document);
        }
    }

    private void SaveUnlocked(CreativeProjectDocument document)
    {
        document.SchemaVersion = CreativeProjectDocument.CurrentSchemaVersion;
        document.Projects ??= [];
        foreach (var project in document.Projects)
        {
            project.Resources ??= [];
        }

        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
