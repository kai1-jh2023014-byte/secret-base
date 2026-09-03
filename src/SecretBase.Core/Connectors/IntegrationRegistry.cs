namespace SecretBase.Core.Connectors;

public sealed class IntegrationRegistration
{
    public IntegrationManifest Manifest { get; set; } = new();

    public bool Enabled { get; set; } = true;

    public bool Approved { get; set; }

    public IntegrationPermissionKind Permissions { get; set; } = IntegrationPermissionKind.Read;

    /// <summary>Opaque handle. Never a token, key, or password.</summary>
    public string? CredentialReference { get; set; }

    public DateTimeOffset RegisteredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedAt { get; set; }

    public IntegrationHealthStatus Health { get; set; } = IntegrationHealthStatus.Disconnected;

    public string? HealthDetail { get; set; }

    public IntegrationDiscoveryKind Discovery { get; set; } = IntegrationDiscoveryKind.ExplicitRegistration;

    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Id => Manifest.Id;

    public string DisplayName => string.IsNullOrWhiteSpace(Manifest.Name) ? Manifest.Id : Manifest.Name;
}

public sealed class IntegrationRegistryDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<IntegrationRegistration> Items { get; set; } = [];
}

public interface IIntegrationRegistry
{
    IReadOnlyList<IntegrationRegistration> List();

    IntegrationRegistration? Find(string? id);

    bool TryRegister(IntegrationManifest manifest, bool approved, IntegrationDiscoveryKind discovery, out string error);

    bool Unregister(string id);

    bool SetEnabled(string id, bool enabled);

    bool SetPermissions(string id, IntegrationPermissionKind permissions);

    bool SetCredentialReference(string id, string? credentialReference);

    void Touch(string id, DateTimeOffset at, IntegrationHealthStatus health, string? detail = null);

    IntegrationRegistryDocument Snapshot();

    void ReplaceAll(IEnumerable<IntegrationRegistration> items);
}

public sealed class IntegrationRegistry : IIntegrationRegistry
{
    private readonly object _gate = new();
    private readonly List<IntegrationRegistration> _items = [];

    public IReadOnlyList<IntegrationRegistration> List()
    {
        lock (_gate)
        {
            return _items.Select(Clone).ToList();
        }
    }

    public IntegrationRegistration? Find(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        lock (_gate)
        {
            var found = _items.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            return found is null ? null : Clone(found);
        }
    }

    public bool TryRegister(
        IntegrationManifest manifest,
        bool approved,
        IntegrationDiscoveryKind discovery,
        out string error)
    {
        error = string.Empty;
        if (!IntegrationManifestValidator.TryValidate(manifest, out error))
        {
            return false;
        }

        lock (_gate)
        {
            if (_items.Any(item => item.Id.Equals(manifest.Id, StringComparison.OrdinalIgnoreCase)))
            {
                error = "An integration with this id is already registered.";
                return false;
            }

            _items.Add(new IntegrationRegistration
            {
                Manifest = manifest,
                Approved = approved,
                Enabled = approved,
                Permissions = approved
                    ? IntegrationPermissionGate.DefaultFor(manifest)
                    : IntegrationPermissionKind.None,
                Discovery = discovery,
                Health = approved ? IntegrationHealthStatus.Disconnected : IntegrationHealthStatus.Disabled,
                RegisteredAt = DateTimeOffset.UtcNow
            });
            return true;
        }
    }

    public bool Unregister(string id)
    {
        lock (_gate)
        {
            return _items.RemoveAll(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
        }
    }

    public bool SetEnabled(string id, bool enabled)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return false;
            }

            item.Enabled = enabled && item.Approved;
            item.Health = item.Enabled ? IntegrationHealthStatus.Disconnected : IntegrationHealthStatus.Disabled;
            return true;
        }
    }

    public bool SetPermissions(string id, IntegrationPermissionKind permissions)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return false;
            }

            item.Permissions = permissions;
            return true;
        }
    }

    public bool SetCredentialReference(string id, string? credentialReference)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return false;
            }

            item.CredentialReference = string.IsNullOrWhiteSpace(credentialReference)
                ? null
                : CredentialReference.Normalize(credentialReference);
            return true;
        }
    }

    public void Touch(string id, DateTimeOffset at, IntegrationHealthStatus health, string? detail = null)
    {
        lock (_gate)
        {
            var item = _items.FirstOrDefault(candidate => candidate.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return;
            }

            item.LastUsedAt = at;
            item.Health = health;
            item.HealthDetail = detail;
        }
    }

    public IntegrationRegistryDocument Snapshot()
    {
        lock (_gate)
        {
            return new IntegrationRegistryDocument
            {
                Schema = IntegrationRegistryDocument.SchemaVersion,
                Items = _items.Select(Clone).ToList()
            };
        }
    }

    public void ReplaceAll(IEnumerable<IntegrationRegistration> items)
    {
        lock (_gate)
        {
            _items.Clear();
            foreach (var item in items)
            {
                if (item.Manifest is not null
                    && IntegrationManifestValidator.TryValidate(item.Manifest, out _))
                {
                    _items.Add(Clone(item));
                }
            }
        }
    }

    private static IntegrationRegistration Clone(IntegrationRegistration item) => new()
    {
        Manifest = item.Manifest,
        Enabled = item.Enabled,
        Approved = item.Approved,
        Permissions = item.Permissions,
        CredentialReference = item.CredentialReference,
        RegisteredAt = item.RegisteredAt,
        LastUsedAt = item.LastUsedAt,
        Health = item.Health,
        HealthDetail = item.HealthDetail,
        Discovery = item.Discovery,
        Settings = new Dictionary<string, string>(item.Settings, StringComparer.OrdinalIgnoreCase)
    };
}
