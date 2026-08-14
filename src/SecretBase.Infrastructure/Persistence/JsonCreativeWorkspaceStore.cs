using System.Text.Json;
using SecretBase.Core.Creative;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>
/// Persists Creative Workspace under AppData/creative/workspace.json (atomic write).
/// </summary>
public sealed class JsonCreativeWorkspaceStore : ICreativeWorkspaceStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonCreativeWorkspaceStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.CreativeDirectory, "workspace.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public CreativeWorkspaceDocument LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = new CreativeWorkspaceDocument();
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<CreativeWorkspaceDocument>(json, _options)
                          ?? new CreativeWorkspaceDocument();
                if (doc.SchemaVersion < 1)
                {
                    doc.SchemaVersion = CreativeWorkspaceDocument.CurrentSchemaVersion;
                }

                doc.Items ??= [];
                // Drop invalid entries on load.
                doc.Items = doc.Items
                    .Where(i => i is not null
                                && !string.IsNullOrWhiteSpace(i.Name)
                                && CreativePathValidator.TryNormalize(i.Path, i.ItemType, out _, out _))
                    .ToList();
                return doc;
            }
            catch (JsonException)
            {
                return new CreativeWorkspaceDocument();
            }
            catch (IOException)
            {
                return new CreativeWorkspaceDocument();
            }
        }
    }

    public void Save(CreativeWorkspaceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            SaveUnlocked(document);
        }
    }

    private void SaveUnlocked(CreativeWorkspaceDocument document)
    {
        document.SchemaVersion = CreativeWorkspaceDocument.CurrentSchemaVersion;
        document.Items ??= [];
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
