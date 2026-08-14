namespace SecretBase.Core.Creative;

/// <summary>Result of a CreativeCommand. Open does not launch — App/Platform does after validation.</summary>
public sealed class CreativeCommandResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public CreativeCommandKind Kind { get; init; }

    public IReadOnlyList<CreativeItem> Items { get; init; } = Array.Empty<CreativeItem>();

    public CreativeItem? Item { get; init; }

    /// <summary>When true, host may call ITargetLaunchService for <see cref="Item"/>.</summary>
    public bool ShouldLaunch { get; init; }

    public static CreativeCommandResult Ok(
        CreativeCommandKind kind,
        IReadOnlyList<CreativeItem>? items = null,
        CreativeItem? item = null,
        bool shouldLaunch = false) =>
        new()
        {
            Succeeded = true,
            Kind = kind,
            Items = items ?? Array.Empty<CreativeItem>(),
            Item = item,
            ShouldLaunch = shouldLaunch
        };

    public static CreativeCommandResult Fail(CreativeCommandKind kind, string error) =>
        new()
        {
            Succeeded = false,
            Kind = kind,
            ErrorMessage = error
        };
}
