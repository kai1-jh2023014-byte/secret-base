namespace SecretBase.Core.Context;

public sealed class StaticContextProvider : IBaseContextProvider
{
    private readonly Func<BaseContextSlice> _factory;

    public StaticContextProvider(string sliceId, Func<BaseContextSlice> factory)
    {
        SliceId = sliceId;
        _factory = factory;
    }

    public string SliceId { get; }

    public BaseContextSlice GetSlice() => _factory();
}
