namespace SecretBase.Core.Blocks;

/// <summary>How Block item icons are painted.</summary>
public static class BlockIconStyle
{
    /// <summary>Full-color shell / custom icons (default).</summary>
    public const string Color = "color";

    /// <summary>White silhouette — fashionable monochrome rail look.</summary>
    public const string Silhouette = "silhouette";

    public static string Normalize(string? value) =>
        string.Equals(value, Silhouette, StringComparison.OrdinalIgnoreCase)
            ? Silhouette
            : Color;

    public static bool IsSilhouette(string? value) =>
        string.Equals(Normalize(value), Silhouette, StringComparison.Ordinal);
}
