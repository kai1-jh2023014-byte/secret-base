namespace SecretBase.Core.Progress;

/// <summary>In-memory provider for unit tests and demos.</summary>
public sealed class MemoryProgressGenesisProvider : IProgressGenesisProvider, IProgressGenesisStore
{
    private readonly object _gate = new();
    private ProgressGenesisSnapshot _snapshot;

    public MemoryProgressGenesisProvider(ProgressGenesisSnapshot? snapshot = null)
    {
        _snapshot = snapshot ?? ProgressGenesisSnapshot.CreateDemoSeed();
        _snapshot.SourceKind = ProgressGenesisSourceKinds.Memory;
        _snapshot.Normalize();
    }

    public string ProviderId => "memory";

    public string DisplayName => "Memory";

    public string SourceKind => ProgressGenesisSourceKinds.Memory;

    public ProgressGenesisSnapshot LoadOrCreate()
    {
        lock (_gate)
        {
            _snapshot.Normalize();
            return Clone(_snapshot);
        }
    }

    public void Save(ProgressGenesisSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            snapshot.Normalize();
            snapshot.SourceKind = ProgressGenesisSourceKinds.Memory;
            _snapshot = Clone(snapshot);
        }
    }

    public Task<ProgressGenesisSnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadOrCreate());
    }

    private static ProgressGenesisSnapshot Clone(ProgressGenesisSnapshot source)
    {
        source.Normalize();
        return new ProgressGenesisSnapshot
        {
            Schema = source.Schema,
            UpdatedAt = source.UpdatedAt,
            SourceKind = source.SourceKind,
            Progress = new ProgressTrack
            {
                Title = source.Progress.Title,
                Percent = source.Progress.Percent,
                Status = source.Progress.Status,
                Detail = source.Progress.Detail
            },
            Genesis = new GenesisTrack
            {
                Title = source.Genesis.Title,
                Phase = source.Genesis.Phase,
                Stage = source.Genesis.Stage,
                StageCount = source.Genesis.StageCount,
                Percent = source.Genesis.Percent,
                Status = source.Genesis.Status,
                Milestones = source.Genesis.Milestones
                    .Select(m => new GenesisMilestone
                    {
                        Id = m.Id,
                        Label = m.Label,
                        IsComplete = m.IsComplete
                    })
                    .ToList()
            }
        };
    }
}
