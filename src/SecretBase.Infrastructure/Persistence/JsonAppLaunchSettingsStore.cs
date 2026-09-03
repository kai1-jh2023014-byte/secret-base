using System.Text.Json;
using SecretBase.Core;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonAppLaunchSettingsStore : IAppLaunchSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonAppLaunchSettingsStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "launch.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public string FilePath => _path;

    public AppLaunchSettings LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = CreateDefault();
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<AppLaunchSettings>(json, _options)
                          ?? CreateDefault();
                return Normalize(doc);
            }
            catch (JsonException)
            {
                return CreateDefault();
            }
            catch (IOException)
            {
                return CreateDefault();
            }
        }
    }

    public void Save(AppLaunchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            SaveUnlocked(Normalize(settings));
        }
    }

    private static AppLaunchSettings CreateDefault() =>
        new() { SchemaVersion = AppLaunchSettings.CurrentSchemaVersion };

    private static AppLaunchSettings Normalize(AppLaunchSettings settings)
    {
        if (settings.SchemaVersion < 2)
        {
            settings.TaskbarAiChatEnabled = true;
            settings.SchemaVersion = 2;
        }

        settings.SchemaVersion = AppLaunchSettings.CurrentSchemaVersion;
        return settings;
    }

    private void SaveUnlocked(AppLaunchSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
