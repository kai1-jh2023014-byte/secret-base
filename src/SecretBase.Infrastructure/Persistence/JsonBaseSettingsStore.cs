using System.Text.Json;
using SecretBase.Core.Base;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonBaseSettingsStore : IBaseSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonBaseSettingsStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "base.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public BaseSettings LoadOrCreate(bool layoutAlreadyExisted)
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = BaseSettingsMigrator.MigrateToCurrent(null, layoutAlreadyExisted);
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var loaded = JsonSerializer.Deserialize<BaseSettings>(json, _options);
                return BaseSettingsMigrator.MigrateToCurrent(loaded, layoutAlreadyExisted: false);
            }
            catch (JsonException)
            {
                return BaseSettingsMigrator.MigrateToCurrent(null, layoutAlreadyExisted);
            }
            catch (IOException)
            {
                return BaseSettingsMigrator.MigrateToCurrent(null, layoutAlreadyExisted);
            }
        }
    }

    public void Save(BaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            SaveUnlocked(settings);
        }
    }

    private void SaveUnlocked(BaseSettings settings)
    {
        settings.Schema = BaseSettings.SchemaVersion;
        settings.SelectedModules ??= [];
        settings.RecentAppNames ??= [];
        var json = JsonSerializer.Serialize(settings, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
