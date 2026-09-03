using System.Text.Json;
using SecretBase.Core.Activity;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonActivityStore : IActivityLog
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly ActivityLog _inner;
    private readonly object _gate = new();

    public JsonActivityStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "activity.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _inner = new ActivityLog();
        _inner.ReplaceAll(LoadUnlocked().Events);
    }

    public void Record(ActivityEvent activity)
    {
        lock (_gate)
        {
            _inner.Record(activity);
            PersistUnlocked();
        }
    }

    public IReadOnlyList<ActivityEvent> Recent(int take = 40) => _inner.Recent(take);

    public IReadOnlyList<MeaningfulActivity> Meaningful(DateTimeOffset now, TimeSpan? window = null) =>
        _inner.Meaningful(now, window);

    private ActivityDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new ActivityDocument();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<ActivityDocument>(json, _options) ?? new ActivityDocument();
            document.Events ??= [];
            document.Schema = ActivityDocument.SchemaVersion;
            return document;
        }
        catch (JsonException)
        {
            return new ActivityDocument();
        }
        catch (IOException)
        {
            return new ActivityDocument();
        }
    }

    private void PersistUnlocked()
    {
        var document = _inner.Snapshot();
        document.Schema = ActivityDocument.SchemaVersion;
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
