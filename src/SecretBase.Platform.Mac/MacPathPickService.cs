using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Path picker that delegates to the Mac host (Avalonia StorageProvider). HWND owner is unused.
/// </summary>
public sealed class MacPathPickService : IPathPickService
{
    private readonly Func<Task<PathPickResult>>? _pickFile;
    private readonly Func<Task<PathPickResult>>? _pickFolder;
    private readonly Func<Task<PathPickResult>>? _pickImage;

    public MacPathPickService(
        Func<Task<PathPickResult>>? pickFile = null,
        Func<Task<PathPickResult>>? pickFolder = null,
        Func<Task<PathPickResult>>? pickImage = null)
    {
        _pickFile = pickFile;
        _pickFolder = pickFolder;
        _pickImage = pickImage;
    }

    public void SetOwnerWindow(nint windowHandle)
    {
        _ = windowHandle;
    }

    public Task<PathPickResult> PickFileAsync() =>
        _pickFile?.Invoke()
        ?? Task.FromResult(new PathPickResult(false, false, null, false, "Path picker is not configured."));

    public Task<PathPickResult> PickFolderAsync() =>
        _pickFolder?.Invoke()
        ?? Task.FromResult(new PathPickResult(false, false, null, true, "Path picker is not configured."));

    public Task<PathPickResult> PickImageAsync() =>
        _pickImage?.Invoke()
        ?? _pickFile?.Invoke()
        ?? Task.FromResult(new PathPickResult(false, false, null, false, "Path picker is not configured."));
}
