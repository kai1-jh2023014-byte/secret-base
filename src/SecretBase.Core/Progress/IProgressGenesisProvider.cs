namespace SecretBase.Core.Progress;

/// <summary>
/// Fetches Progress + Genesis advancement. Network/file I/O lives in Infrastructure;
/// Core only defines the contract and models.
/// </summary>
public interface IProgressGenesisProvider
{
    string ProviderId { get; }

    string DisplayName { get; }

    string SourceKind { get; }

    Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Optional writable side for local JSON (not required for HTTP providers).</summary>
public interface IProgressGenesisStore
{
    ProgressGenesisSnapshot LoadOrCreate();

    void Save(ProgressGenesisSnapshot snapshot);
}
