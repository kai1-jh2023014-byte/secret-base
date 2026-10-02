namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Imports user images (and simple design presets) into Secret Base AppData as PNG icons.
/// </summary>
public interface ICustomIconService
{
    /// <summary>Directory under AppData where user/preset icons are stored.</summary>
    string CustomIconsDirectory { get; }

    /// <summary>True when <paramref name="path"/> is a user/preset icon owned by Secret Base.</summary>
    bool IsUserIcon(string? path);

    /// <summary>
    /// Copies or converts an image file into the custom icons folder for a Block item.
    /// </summary>
    bool TryImportImage(string sourcePath, Guid itemId, out string? storedPath, out string? errorMessage);

    /// <summary>Renders a solid-color design tile with a letter glyph for the item.</summary>
    bool TryCreatePresetIcon(
        Guid itemId,
        string presetId,
        string glyph,
        out string? storedPath,
        out string? errorMessage);

    /// <summary>Deletes a previously imported custom icon file when it lives under the custom folder.</summary>
    void TryDeleteUserIcon(string? storedPath);
}
