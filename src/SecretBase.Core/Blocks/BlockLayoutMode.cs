namespace SecretBase.Core.Blocks;

/// <summary>How Block items are arranged inside the Block surface.</summary>
public static class BlockLayoutMode
{
    /// <summary>Labeled grid tiles (default).</summary>
    public const string Grid = "grid";

    /// <summary>Compact icon rail — labels hidden, tight spacing (reference “left of clock” look).</summary>
    public const string Rail = "rail";

    public static string Normalize(string? value) =>
        string.Equals(value, Rail, StringComparison.OrdinalIgnoreCase)
            ? Rail
            : Grid;

    public static bool IsRail(string? value) =>
        string.Equals(Normalize(value), Rail, StringComparison.Ordinal);
}
