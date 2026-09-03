namespace SecretBase.Core.Context;

/// <summary>
/// Named, scoped slice of Base state. Providers must not crawl the disk or
/// send data to a model — they only expose what Secret Base already stores.
/// </summary>
public interface IBaseContextProvider
{
    string SliceId { get; }

    BaseContextSlice GetSlice();
}
