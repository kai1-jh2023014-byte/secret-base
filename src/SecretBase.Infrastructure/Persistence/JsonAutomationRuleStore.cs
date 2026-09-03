using System.Text.Json;
using SecretBase.Core.Automation;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonAutomationRuleStore : IAutomationRuleStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly AutomationRuleStore _inner;
    private readonly object _gate = new();

    public JsonAutomationRuleStore(string? filePath = null, JsonSerializerOptions? options = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "automation-rules.json");
        _options = options ?? SecretBaseJson.CreateOptions();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var loaded = LoadUnlocked();
        _inner = new AutomationRuleStore(loaded.Rules.Count == 0 ? null : loaded.Rules);
    }

    public IReadOnlyList<AutomationRule> List() => _inner.List();

    public void Save(AutomationRule rule)
    {
        lock (_gate)
        {
            _inner.Save(rule);
            PersistUnlocked();
        }
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            _inner.Remove(id);
            PersistUnlocked();
        }
    }

    public void ReplaceAll(IEnumerable<AutomationRule> rules)
    {
        lock (_gate)
        {
            _inner.ReplaceAll(rules);
            PersistUnlocked();
        }
    }

    private AutomationRuleDocument LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return new AutomationRuleDocument();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<AutomationRuleDocument>(json, _options)
                           ?? new AutomationRuleDocument();
            document.Rules ??= [];
            document.Schema = AutomationRuleDocument.SchemaVersion;
            return document;
        }
        catch (JsonException)
        {
            return new AutomationRuleDocument();
        }
        catch (IOException)
        {
            return new AutomationRuleDocument();
        }
    }

    private void PersistUnlocked()
    {
        var document = new AutomationRuleDocument
        {
            Schema = AutomationRuleDocument.SchemaVersion,
            Rules = _inner.List().ToList()
        };
        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
