using Windows.Storage.Pickers;
using WinRT.Interop;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// WinUI / Windows.Storage file and folder pickers. Requires owner HWND.
/// </summary>
public sealed class WindowsPathPickService : IPathPickService
{
    private nint _hwnd;

    public void SetOwnerWindow(nint windowHandle) => _hwnd = windowHandle;

    public async Task<PathPickResult> PickFileAsync()
    {
        if (_hwnd == nint.Zero)
        {
            return new PathPickResult(false, false, null, false, "Owner window is not set.");
        }

        try
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return new PathPickResult(false, true, null, false, null);
            }

            var path = file.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathPickResult(false, false, null, false, "Selected file has no path.");
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
        if (_hwnd == nint.Zero)
        {
            return new PathPickResult(false, false, null, false, "Owner window is not set.");
        }

        try
        {
            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, _hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return new PathPickResult(false, true, null, true, null);
            }

            var path = folder.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                return new PathPickResult(false, false, null, true, "Selected folder has no path.");
            }

            return new PathPickResult(true, false, path, true, null);
        }
        catch (Exception ex)
        {
            return new PathPickResult(false, false, null, true, ex.Message);
        }
    }
}
