namespace SecretBase.Core.Context;

public sealed record BaseContextSlice(
    string Id,
    string Summary,
    IReadOnlyList<string> Facts);
