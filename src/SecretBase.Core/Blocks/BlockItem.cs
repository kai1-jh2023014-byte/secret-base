namespace SecretBase.Core.Blocks;

/// <summary>
/// A launchable entry inside a Block (application, shortcut, file, or folder).
/// Target is an absolute path or user-chosen shell target — never a free-form command line.
/// </summary>
public sealed class BlockItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public BlockItemType Type { get; set; } = BlockItemType.Application;

    /// <summary>Absolute path to the executable, .lnk, file, or folder the user chose.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Optional icon hint (path or glyph key). Empty = type default glyph.</summary>
    public string Icon { get; set; } = string.Empty;
}
