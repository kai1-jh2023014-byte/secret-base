using System.Text.Json;
using SecretBase.Core.Apps;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>
/// Persists My Apps under AppData/apps/apps.json (atomic write).
/// schemaVersion 1. Drops invalid entries; keeps apps whose files are missing on disk.
/// </summary>
public sealed class JsonCustomAppStore : ICustomAppStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonCustomAppStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.AppsDirectory, "apps.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public CustomAppDocument LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = CustomAppDocumentMigrator.MigrateToCurrent(new CustomAppDocument());
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<CustomAppDocument>(json, _options)
                          ?? new CustomAppDocument();
                doc = CustomAppDocumentMigrator.MigrateToCurrent(doc);
                doc.Apps ??= [];
                doc.Apps = doc.Apps.Where(IsPersistable).ToList();
                return doc;
            }
            catch (JsonException)
            {
                return CustomAppDocumentMigrator.MigrateToCurrent(new CustomAppDocument());
            }
            catch (IOException)
            {
                return CustomAppDocumentMigrator.MigrateToCurrent(new CustomAppDocument());
            }
        }
    }

    public void Save(CustomAppDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (_gate)
        {
            SaveUnlocked(document);
        }
    }

    private void SaveUnlocked(CustomAppDocument document)
    {
        document = CustomAppDocumentMigrator.MigrateToCurrent(document);
        document.SchemaVersion = CustomAppDocument.CurrentSchemaVersion;
        document.Apps ??= [];
        document.Apps = document.Apps.Where(IsPersistable).ToList();

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

    private static bool IsPersistable(CustomApp? app)
    {
        if (app is null || string.IsNullOrWhiteSpace(app.Name))
        {
            return false;
        }

        return CustomAppValidator.TryNormalize(
            app.Name,
            app.Description,
            app.Type,
            app.LaunchTarget,
            app.ProjectRoot,
            app.CreativeProjectId,
            out _,
            out _);
    }
}
