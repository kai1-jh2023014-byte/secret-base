using System.Text.Json;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Persists Progress + Genesis to AppData JSON with atomic <c>.tmp</c> → copy → delete writes.
/// Path: <c>%LocalAppData%\SecretBase\settings\progress-genesis.json</c> (macOS: Application Support).
/// </summary>
public sealed class JsonProgressGenesisStore : IProgressGenesisStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _gate = new();

    public JsonProgressGenesisStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "progress-genesis.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public ProgressGenesisSnapshot LoadOrCreate()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                var seeded = ProgressGenesisSnapshot.CreateDemoSeed();
                seeded.SourceKind = ProgressGenesisSourceKinds.LocalJson;
                SaveUnlocked(seeded);
                return Clone(seeded);
            }

            try
            {
                var json = File.ReadAllText(_path);
                var snapshot = JsonSerializer.Deserialize<ProgressGenesisSnapshot>(json, _options)
                    ?? ProgressGenesisSnapshot.CreateDemoSeed();
                snapshot.SourceKind = ProgressGenesisSourceKinds.LocalJson;
                snapshot.Normalize();
                return Clone(snapshot);
            }
            catch (JsonException)
            {
                return ProgressGenesisSnapshot.CreateEmpty(ProgressGenesisSourceKinds.LocalJson);
            }
            catch (IOException)
            {
                return ProgressGenesisSnapshot.CreateEmpty(ProgressGenesisSourceKinds.LocalJson);
            }
        }
    }

    public void Save(ProgressGenesisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            SaveUnlocked(snapshot);
        }
    }

    private void SaveUnlocked(ProgressGenesisSnapshot snapshot)
    {
        snapshot.Normalize();
        snapshot.SourceKind = ProgressGenesisSourceKinds.LocalJson;
        var json = JsonSerializer.Serialize(snapshot, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }

    private static ProgressGenesisSnapshot Clone(ProgressGenesisSnapshot source)
    {
        // Re-serialize round-trip keeps Infrastructure independent of Core clone helpers.
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProgressGenesisSnapshot>(json) ?? ProgressGenesisSnapshot.CreateEmpty();
    }
}
