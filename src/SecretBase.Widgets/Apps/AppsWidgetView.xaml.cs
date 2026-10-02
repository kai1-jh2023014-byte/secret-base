using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Apps;
using SecretBase.Core.Themes;
using SecretBase.Widgets.Hosting;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Apps;

/// <summary>
/// My Apps hub. Registers and launches via host callbacks only.
/// Remove is Secret Base registration only. No Host Bridge.
/// </summary>
public sealed partial class AppsWidgetView : UserControl
{
    private AppCommandService? _commands;
    private Func<AppCommandResult, string?>? _tryExecute;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFile;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFolder;
    private OverlayDialogInput? _dialogInput;
    private ThemeDefinition? _theme;

    public AppsWidgetView()
    {
        InitializeComponent();
    }

    public void Initialize(
        AppCommandService commands,
        Func<AppCommandResult, string?> tryExecute,
        Func<Task<(bool ok, bool cancelled, string? path, string? error)>> pickFile,
        Func<Task<(bool ok, bool cancelled, string? path, string? error)>> pickFolder,
        OverlayDialogInput? dialogInput = null)
    {
        _commands = commands;
        _tryExecute = tryExecute;
        _pickFile = pickFile;
        _pickFolder = pickFolder;
        _dialogInput = dialogInput;
        StatusLabel.Text = string.Empty;
        Rebuild();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyChrome(RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SubtitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyActionButton(AddButton, theme, accent: true);
        Rebuild();
    }

    private void Rebuild()
    {
        AppsList.Children.Clear();
        if (_commands is null)
        {
            return;
        }

        var listed = _commands.Execute(AppCommand.ListApps());
        if (!listed.Succeeded)
        {
            StatusLabel.Text = listed.ErrorMessage ?? "Could not list apps.";
            return;
        }

        if (listed.Apps.Count == 0)
        {
            AppsList.Children.Add(new TextBlock
            {
                Text = "No apps registered yet.",
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            return;
        }

        foreach (var app in listed.Apps)
        {
            AppsList.Children.Add(CreateAppRow(app));
        }
    }

    private UIElement CreateAppRow(CustomApp app)
    {
        var open = new Button
        {
            Content = "Open",
            MinWidth = 56,
            Padding = new Thickness(8, 4, 8, 4),
            Tag = app.Id
        };
        open.Click += OpenApp_Click;
        ToolTipService.SetToolTip(open, "Launch registered target");

        var cursor = new Button
        {
            Content = "Cursor",
            MinWidth = 64,
            Padding = new Thickness(8, 4, 8, 4),
            Tag = app.Id
        };
        cursor.Click += OpenCursor_Click;
        ToolTipService.SetToolTip(cursor, "Open project root in Cursor");

        var remove = new Button
        {
            Content = "×",
            MinWidth = 32,
            Padding = new Thickness(6, 4, 6, 4),
            Tag = app.Id
        };
        remove.Click += RemoveApp_Click;
        ToolTipService.SetToolTip(remove, "Remove registration only");

        var title = new TextBlock
        {
            Text = AppGlyph(app.Type) + "  " + app.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var detail = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(app.Description) ? app.LaunchTarget : app.Description,
            FontSize = 10,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(title);
        text.Children.Add(detail);

        if (_theme is not null)
        {
            StyleActionButton(open, _theme, accent: true);
            StyleActionButton(cursor, _theme);
            StyleActionButton(remove, _theme);
            title.FontFamily = new FontFamily(_theme.FontFamily);
            title.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
            detail.FontFamily = new FontFamily(_theme.FontFamily);
            detail.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        actions.Children.Add(open);
        actions.Children.Add(cursor);
        actions.Children.Add(remove);

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        Grid.SetColumn(actions, 1);
        row.Children.Add(text);
        row.Children.Add(actions);
        return row;
    }

    private static string AppGlyph(CustomAppType type) => type switch
    {
        CustomAppType.Website => "🌐",
        CustomAppType.Folder => "📁",
        _ => "🎮"
    };

    private static void StyleActionButton(Button button, ThemeDefinition theme, bool accent = false) =>
        WidgetSurfaceStyle.ApplyActionButton(button, theme, accent);

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || _tryExecute is null || sender is not Button { Tag: string appId })
        {
            return;
        }

        var result = _commands.Execute(AppCommand.OpenApp(appId));
        StatusLabel.Text = _tryExecute(result) ?? (result.Succeeded ? $"Opened {result.App?.Name}." : (result.ErrorMessage ?? "Open failed."));
    }

    private void OpenCursor_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || _tryExecute is null || sender is not Button { Tag: string appId })
        {
            return;
        }

        var result = _commands.Execute(AppCommand.OpenAppInCursor(appId));
        StatusLabel.Text = _tryExecute(result) ?? (result.Succeeded ? "Opened in Cursor." : (result.ErrorMessage ?? "Cursor open failed."));
        Rebuild();
    }

    private void RemoveApp_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || sender is not Button { Tag: string appId })
        {
            return;
        }

        if (_commands.Apps.TryRemove(appId, out var error))
        {
            StatusLabel.Text = "Removed registration (files were not deleted).";
            Rebuild();
            return;
        }

        StatusLabel.Text = error;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e) =>
        await ShowRegisterDialogAsync();

    private async Task ShowRegisterDialogAsync()
    {
        if (_commands is null || _pickFile is null || _pickFolder is null)
        {
            return;
        }

        var nameBox = new TextBox { Header = "Name", PlaceholderText = "Pokemon Calculator" };
        var descriptionBox = new TextBox { Header = "Description", PlaceholderText = "Optional" };
        var typeBox = new ComboBox
        {
            Header = "Type",
            ItemsSource = new[] { "Application", "Folder", "Website" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var targetBox = new TextBox { Header = "Launch target", PlaceholderText = @"C:\Apps\game.exe or https://…" };
        var pickTarget = new Button { Content = "Pick…", MinWidth = 72 };
        pickTarget.Click += async (_, _) =>
        {
            var selectedType = typeBox.SelectedItem as string;
            if (string.Equals(selectedType, "Website", StringComparison.Ordinal))
            {
                return;
            }

            var picker = string.Equals(selectedType, "Folder", StringComparison.Ordinal) ? _pickFolder : _pickFile;
            var picked = await picker();
            if (picked.ok && !string.IsNullOrWhiteSpace(picked.path))
            {
                targetBox.Text = picked.path;
            }
        };

        var targetRow = new Grid { ColumnSpacing = 6 };
        targetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        targetRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(targetBox, 0);
        Grid.SetColumn(pickTarget, 1);
        targetRow.Children.Add(targetBox);
        targetRow.Children.Add(pickTarget);

        var rootBox = new TextBox { Header = "Project root (optional, for Cursor)", PlaceholderText = @"C:\src\pokemon-calc" };
        var pickRoot = new Button { Content = "Pick…", MinWidth = 72 };
        pickRoot.Click += async (_, _) =>
        {
            var picked = await _pickFolder();
            if (picked.ok && !string.IsNullOrWhiteSpace(picked.path))
            {
                rootBox.Text = picked.path;
            }
        };

        var rootRow = new Grid { ColumnSpacing = 6 };
        rootRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rootRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(rootBox, 0);
        Grid.SetColumn(pickRoot, 1);
        rootRow.Children.Add(rootBox);
        rootRow.Children.Add(pickRoot);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(nameBox);
        panel.Children.Add(descriptionBox);
        panel.Children.Add(typeBox);
        panel.Children.Add(targetRow);
        panel.Children.Add(rootRow);

        var dialog = new ContentDialog
        {
            Title = "Register app",
            PrimaryButtonText = "Register",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        using var _ = _dialogInput?.Enter();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var type = (typeBox.SelectedItem as string) switch
        {
            "Folder" => CustomAppType.Folder,
            "Website" => CustomAppType.Website,
            _ => CustomAppType.Application
        };

        var draft = new CustomApp
        {
            Name = nameBox.Text ?? string.Empty,
            Description = descriptionBox.Text,
            Type = type,
            LaunchTarget = targetBox.Text ?? string.Empty,
            ProjectRoot = rootBox.Text
        };

        if (_commands.Apps.TryAdd(draft, out var saved, out var error))
        {
            StatusLabel.Text = $"Registered {saved!.Name}.";
            Rebuild();
            return;
        }

        StatusLabel.Text = error;
    }
}
