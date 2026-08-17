using System.Text.Json;
using SecretBase.Core.Assistant;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

/// <summary>Persists provider/model only. API keys stay in ISecureSecretStore.</summary>
public sealed class JsonAssistantSettingsStore : IAssistantSettingsStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonAssistantSettingsStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "assistant.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public AssistantSettings LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var created = AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings());
                SaveUnlocked(created);
                return created;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var doc = JsonSerializer.Deserialize<AssistantSettings>(json, _options)
                          ?? new AssistantSettings();
                return AssistantSettingsMigrator.MigrateToCurrent(doc);
            }
            catch (JsonException)
            {
                return AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings());
            }
            catch (IOException)
            {
                return AssistantSettingsMigrator.MigrateToCurrent(new AssistantSettings());
            }
        }
    }

    public void Save(AssistantSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (_gate)
        {
            SaveUnlocked(settings);
        }
    }

    private void SaveUnlocked(AssistantSettings settings)
    {
        var doc = AssistantSettingsMigrator.MigrateToCurrent(settings);
        doc.SchemaVersion = AssistantSettings.CurrentSchemaVersion;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(doc, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
