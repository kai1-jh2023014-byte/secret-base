using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace SecretBase.App;

/// <summary>
/// Minimal startup failure UI. Does not modify user data or shell state.
/// </summary>
internal static class StartupFailurePresenter
{
    public static void ShowBlocking(string? detail, string dataFolderPath, bool silentUi = false)
    {
        if (silentUi)
        {
            return;
        }

        try
        {
            ShowBlockingAsync(detail, dataFolderPath).ConfigureAwait(true).GetAwaiter().GetResult();
        }
        catch
        {
            // Secondary failure must not throw — caller will exit the process.
        }
    }

    private static async Task ShowBlockingAsync(string? detail, string dataFolderPath)
    {
        var window = new Window();
        var root = new Grid();
        window.Content = root;
        window.Activate();

        var message = string.IsNullOrWhiteSpace(detail)
            ? "Secret Base could not start."
            : $"Secret Base could not start.\n\n{detail}";

        var body = new TextBlock
        {
            Text =
                message
                + "\n\nYour data was not deleted."
                + "\nPlease restart the app."
                + "\nIf the problem continues, reset the affected settings from the data folder.",
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var dialog = new ContentDialog
        {
            Title = "Secret Base",
            Content = body,
            PrimaryButtonText = "Open Data Folder",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = root.XamlRoot
        };

        var result = await dialog.ShowAsync().AsTask().ConfigureAwait(true);
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                await Launcher.LaunchFolderPathAsync(dataFolderPath).AsTask().ConfigureAwait(true);
            }
            catch
            {
                // User can open the folder manually from the path in logs.
            }
        }
    }
}
