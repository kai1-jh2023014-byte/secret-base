namespace SecretBase.Core.Context;

/// <summary>
/// Composes <see cref="IBaseContextProvider"/> slices with a short in-memory cache.
/// Event-driven callers should reuse this instead of polling an LLM.
/// </summary>
public sealed class BaseContextAggregator
{
    public static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromSeconds(15);

    private readonly IReadOnlyList<IBaseContextProvider> _providers;
    private readonly TimeSpan _cacheDuration;
    private readonly object _gate = new();
    private BaseContextSnapshot? _cached;
    private DateTimeOffset _cachedAt;

    public BaseContextAggregator(
        IEnumerable<IBaseContextProvider> providers,
        TimeSpan? cacheDuration = null)
    {
        _providers = providers.ToList();
        _cacheDuration = cacheDuration ?? DefaultCacheDuration;
    }

    public BaseContextSnapshot GetSnapshot(DateTimeOffset now, bool forceRefresh = false)
    {
        lock (_gate)
        {
            if (!forceRefresh
                && _cached is not null
                && now - _cachedAt < _cacheDuration)
            {
                return _cached;
            }

            var slices = new List<BaseContextSlice>(_providers.Count);
            foreach (var provider in _providers)
            {
                try
                {
                    slices.Add(provider.GetSlice());
                }
                catch (Exception)
                {
                    slices.Add(new BaseContextSlice(provider.SliceId, "unavailable", []));
                }
            }

            _cached = new BaseContextSnapshot(now, slices);
            _cachedAt = now;
            return _cached;
        }
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
        }
    }
}
