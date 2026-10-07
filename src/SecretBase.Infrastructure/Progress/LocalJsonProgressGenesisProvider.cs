using SecretBase.Core.Progress;

namespace SecretBase.Infrastructure.Progress;

/// <summary>Reads Progress + Genesis from the local AppData JSON store.</summary>
public sealed class LocalJsonProgressGenesisProvider : IProgressGenesisProvider
{
    private readonly IProgressGenesisStore _store;

    public LocalJsonProgressGenesisProvider(IProgressGenesisStore? store = null)
    {
        _store = store ?? new JsonProgressGenesisStore();
    }

    public string ProviderId => "local-json";

    public string DisplayName => "Local JSON";

    public string SourceKind => ProgressGenesisSourceKinds.LocalJson;

    public Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _store.LoadOrCreate();
        snapshot.SourceKind = ProgressGenesisSourceKinds.LocalJson;
        snapshot.Normalize();
        return Task.FromResult(snapshot);
    }
}
