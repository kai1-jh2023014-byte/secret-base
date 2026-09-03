using System.Text.Json;
using SecretBase.Core.Connectors;
using SecretBase.Infrastructure.Logging;
using SecretBase.Infrastructure.Storage;

namespace SecretBase.Infrastructure.Persistence;

public sealed class JsonIntegrationRegistryStore : IIntegrationRegistry
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly IntegrationRegistry _inner = new();
    private readonly IAppLogger? _logger;
    private readonly object _gate = new();

    public JsonIntegrationRegistryStore(string? filePath = null, IAppLogger? logger = null)
    {
        _path = filePath ?? Path.Combine(AppDataPaths.SettingsDirectory, "integrations.json");
        _options = SecretBaseJson.CreateOptions();
        _logger = logger;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        LoadUnlocked();
    }

    public string FilePath => _path;

    public IReadOnlyList<IntegrationRegistration> List() => _inner.List();

    public IntegrationRegistration? Find(string? id) => _inner.Find(id);

    public bool TryRegister(
        IntegrationManifest manifest,
        bool approved,
        IntegrationDiscoveryKind discovery,
        out string error)
    {
        lock (_gate)
        {
            var ok = _inner.TryRegister(manifest, approved, discovery, out error);
            if (ok)
            {
                PersistUnlocked();
            }

            return ok;
        }
    }

    public bool Unregister(string id)
    {
        lock (_gate)
        {
            var ok = _inner.Unregister(id);
            if (ok)
            {
                PersistUnlocked();
            }

            return ok;
        }
    }

    public bool SetEnabled(string id, bool enabled)
    {
        lock (_gate)
        {
            var ok = _inner.SetEnabled(id, enabled);
            if (ok)
            {
                PersistUnlocked();
            }

            return ok;
        }
    }

    public bool SetPermissions(string id, IntegrationPermissionKind permissions)
    {
        lock (_gate)
        {
            var ok = _inner.SetPermissions(id, permissions);
            if (ok)
            {
                PersistUnlocked();
            }

            return ok;
        }
    }

    public bool SetCredentialReference(string id, string? credentialReference)
    {
        lock (_gate)
        {
            var ok = _inner.SetCredentialReference(id, credentialReference);
            if (ok)
            {
                PersistUnlocked();
            }

            return ok;
        }
    }

    public void Touch(string id, DateTimeOffset at, IntegrationHealthStatus health, string? detail = null)
    {
        lock (_gate)
        {
            _inner.Touch(id, at, health, detail);
            PersistUnlocked();
        }
    }

    public IntegrationRegistryDocument Snapshot() => _inner.Snapshot();

    public void ReplaceAll(IEnumerable<IntegrationRegistration> items)
    {
        lock (_gate)
        {
            _inner.ReplaceAll(items);
            PersistUnlocked();
        }
    }

    private void LoadUnlocked()
    {
        if (!File.Exists(_path))
        {
            DemoManifests.TryRegisterKnown(_inner, approveDemos: false);
            PersistUnlocked();
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var document = JsonSerializer.Deserialize<IntegrationRegistryDocument>(json, _options)
                           ?? new IntegrationRegistryDocument();
            document.Items ??= [];
            if (document.Schema < IntegrationRegistryDocument.SchemaVersion)
            {
                document.Schema = IntegrationRegistryDocument.SchemaVersion;
            }

            _inner.ReplaceAll(document.Items);
            if (_inner.List().Count == 0)
            {
                DemoManifests.TryRegisterKnown(_inner, approveDemos: false);
            }
        }
        catch (JsonException)
        {
            CorruptJsonFileRecovery.TryBackupCorruptFile(_path, _logger, "integrations");
            DemoManifests.TryRegisterKnown(_inner, approveDemos: false);
        }
        catch (IOException)
        {
            DemoManifests.TryRegisterKnown(_inner, approveDemos: false);
        }
    }

    private void PersistUnlocked()
    {
        var document = _inner.Snapshot();
        document.Schema = IntegrationRegistryDocument.SchemaVersion;
        foreach (var item in document.Items)
        {
            item.CredentialReference = string.IsNullOrWhiteSpace(item.CredentialReference)
                ? null
                : CredentialReference.Normalize(item.CredentialReference);
            if (item.CredentialReference is not null
                && CredentialReference.LooksLikeSecret(item.CredentialReference)
                && !item.CredentialReference.StartsWith(CredentialReference.Prefix, StringComparison.OrdinalIgnoreCase))
            {
                item.CredentialReference = null;
            }
        }

        var json = JsonSerializer.Serialize(document, _options);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, json);
        File.Copy(temp, _path, overwrite: true);
        File.Delete(temp);
    }
}
