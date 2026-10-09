using System.Text.Json;
using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Persistence;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Progress;

/// <summary>
/// Append-only Progress / Genesis log.
/// Path: <c>%LocalAppData%\SecretBase\settings\progress-genesis-log.json</c>.
/// Atomic write: <c>.tmp</c> → copy → delete.
/// </summary>
public sealed class JsonProgressGenesisHistoryStore : IProgressGenesisHistoryStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly int _maxEntries;
    private readonly object _gate = new();

    public JsonProgressGenesisHistoryStore(
        string? filePath = null,
        JsonSerializerOptions? options = null,
        int maxEntries = ProgressGenesisHistoryDocument.DefaultMaxEntries)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "progress-genesis-log.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        _maxEntries = Math.Clamp(maxEntries, 8, 500);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string FilePath => _path;

    public ProgressGenesisHistoryDocument LoadOrCreate()
    {
        lock (_gate)
        {
            return LoadUnlocked();
        }
    }

    public ProgressGenesisSnapshot? LoadLatest()
    {
        lock (_gate)
        {
            return LoadUnlocked().LatestSnapshot();
        }
    }

    public bool AppendIfChanged(ProgressGenesisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            var doc = LoadUnlocked();
            var latest = doc.LatestSnapshot();
            if (!ProgressGenesisHistoryComparer.IsMeaningfullyDifferent(latest, snapshot))
            {
                return false;
            }

            var clone = Clone(snapshot);
            clone.Normalize();
            doc.Entries.Add(new ProgressGenesisHistoryEntry
            {
                RecordedAt = DateTimeOffset.UtcNow,
                Snapshot = clone
            });
            doc.Normalize(_maxEntries);
            SaveUnlocked(doc);
            return true;
        }
    }

    private ProgressGenesisHistoryDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            var created = new ProgressGenesisHistoryDocument();
            created.Normalize(_maxEntries);
            return created;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var doc = JsonSerializer.Deserialize<ProgressGenesisHistoryDocument>(json, _options)
                      ?? new ProgressGenesisHistoryDocument();
            doc.Normalize(_maxEntries);
            return doc;
        }
        catch (JsonException)
        {
            return new ProgressGenesisHistoryDocument();
        }
        catch (IOException)
        {
            return new ProgressGenesisHistoryDocument();
        }
    }

    private void SaveUnlocked(ProgressGenesisHistoryDocument document)
    {
        document.Normalize(_maxEntries);
        document.SchemaVersion = ProgressGenesisHistoryDocument.CurrentSchemaVersion;
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }

    private static ProgressGenesisSnapshot Clone(ProgressGenesisSnapshot source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<ProgressGenesisSnapshot>(json)
               ?? ProgressGenesisSnapshot.CreateEmpty();
    }
}
