using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Calendar;

/// <summary>In-memory secret store for tests (never used for production tokens).</summary>
public sealed class MemorySecureSecretStore : ISecureSecretStore
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public bool TryGetSecret(string key, out string? value) => _secrets.TryGetValue(key, out value);

    public void SetSecret(string key, string value) => _secrets[key] = value;

    public void DeleteSecret(string key) => _secrets.Remove(key);
}
