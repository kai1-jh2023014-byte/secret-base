namespace SecretBase.Platform.Abstractions;

/// <summary>
/// OS-backed secret storage for OAuth refresh tokens etc.
/// Implementations must never log secret values.
/// </summary>
public interface ISecureSecretStore
{
    bool TryGetSecret(string key, out string? value);

    void SetSecret(string key, string value);

    void DeleteSecret(string key);
}
