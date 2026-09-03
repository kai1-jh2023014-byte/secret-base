using System.Text.Json;
using SecretBase.Core.Automation;
using SecretBase.Core.Intent;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonAutomationFeedbackStore : IAutomationFeedbackStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly AutomationFeedbackStore _inner;
    private readonly object _gate = new();

    public JsonAutomationFeedbackStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "automation-feedback.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _inner = new AutomationFeedbackStore();
        _inner.ReplaceAll(LoadUnlocked().Items);
    }

    public void Record(AutomationFeedback feedback)
    {
        lock (_gate)
        {
            _inner.Record(feedback);
            PersistUnlocked();
        }
    }

    public IReadOnlyList<AutomationFeedback> Recent(int take = 40) => _inner.Recent(take);

    public double AcceptanceRate(DetectedIntentKind intent) => _inner.AcceptanceRate(intent);

    private AutomationFeedbackDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new AutomationFeedbackDocument();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<AutomationFeedbackDocument>(json, _options)
                           ?? new AutomationFeedbackDocument();
            document.Items ??= [];
            document.Schema = AutomationFeedbackDocument.SchemaVersion;
            return document;
        }
        catch (JsonException)
        {
            return new AutomationFeedbackDocument();
        }
        catch (IOException)
        {
            return new AutomationFeedbackDocument();
        }
    }

    private void PersistUnlocked()
    {
        var document = _inner.Snapshot();
        document.Schema = AutomationFeedbackDocument.SchemaVersion;
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
