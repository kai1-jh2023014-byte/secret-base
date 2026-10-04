using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SecretBase.Platform.Abstractions;

namespace SecretBase.App.Mac;

/// <summary>
/// Avalonia StorageProvider picker. Replaces HWND-based Windows.Storage pickers on macOS.
/// </summary>
public sealed class AvaloniaPathPickService : IPathPickService
{
    private Window? _window;

    public void SetOwner(Window window) => _window = window;

    public void SetOwnerWindow(nint windowHandle) => _ = windowHandle;

    public async Task<PathPickResult> PickFileAsync()
    {
        if (_window is null)
        {
            return new PathPickResult(false, false, null, false, "Owner window is not set.");
        }

        try
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose a file",
                AllowMultiple = false
            });
            var file = files.Count > 0 ? files[0] : null;
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathPickResult(false, file is null, null, false, file is null ? null : "Selected file has no path.");
            }

            return new PathPickResult(true, false, path, false, null);
        }
        catch (Exception ex)
        {
            return new PathPickResult(false, false, null, false, ex.Message);
        }
    }

    public async Task<PathPickResult> PickFolderAsync()
    {
        if (_window is null)
        {
            return new PathPickResult(false, false, null, true, "Owner window is not set.");
        }

        try
        {
            var folders = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose a folder",
                AllowMultiple = false
            });
            var folder = folders.Count > 0 ? folders[0] : null;
            var path = folder?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathPickResult(false, folder is null, null, true, folder is null ? null : "Selected folder has no path.");
            }

            return new PathPickResult(true, false, path, true, null);
        }
        catch (Exception ex)
        {
            return new PathPickResult(false, false, null, true, ex.Message);
        }
    }

    public async Task<PathPickResult> PickImageAsync()
    {
        if (_window is null)
        {
            return new PathPickResult(false, false, null, false, "Owner window is not set.");
        }

        try
        {
            var files = await _window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose an image",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.jfif", "*.bmp", "*.gif", "*.webp", "*.ico", "*.tif", "*.tiff"],
                        AppleUniformTypeIdentifiers = ["public.image"],
                        MimeTypes = ["image/*"]
                    }
                ]
            });
            var file = files.Count > 0 ? files[0] : null;
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathPickResult(false, file is null, null, false, file is null ? null : "Selected image has no path.");
            }

            return new PathPickResult(true, false, path, false, null);
        }
        catch (Exception ex)
        {
            return new PathPickResult(false, false, null, false, ex.Message);
        }
    }
}
