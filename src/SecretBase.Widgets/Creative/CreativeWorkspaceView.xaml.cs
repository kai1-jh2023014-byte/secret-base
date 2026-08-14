using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Creative;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Creative;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Creative;

/// <summary>
/// Creative Workspace UI. Opens only registered items via host launch callback.
/// No delete/move/rename. No Host Bridge.
/// </summary>
public sealed partial class CreativeWorkspaceView : UserControl
{
    private CreativeWorkspaceWidgetConfiguration _configuration =
        CreativeWorkspaceWidgetConfiguration.CreateDefault();
    private CreativeCommandService? _commands;
    private Func<CreativeItem, string?>? _tryLaunch;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFile;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFolder;
    private Action<CreativeWorkspaceWidgetConfiguration>? _onConfigurationChanged;
    private ThemeDefinition? _theme;
    private string _filter = string.Empty;

    public CreativeWorkspaceView()
    {
        InitializeComponent();
    }

    public void Initialize(
        CreativeCommandService commands,
        CreativeWorkspaceWidgetConfiguration configuration,
        Func<CreativeItem, string?> tryLaunch,
        Func<Task<(bool ok, bool cancelled, string? path, string? error)>> pickFile,
        Func<Task<(bool ok, bool cancelled, string? path, string? error)>> pickFolder,
        Action<CreativeWorkspaceWidgetConfiguration>? onConfigurationChanged = null)
    {
        _commands = commands;
        _configuration = configuration;
        _tryLaunch = tryLaunch;
        _pickFile = pickFile;
        _pickFolder = pickFolder;
        _onConfigurationChanged = onConfigurationChanged;
        StatusLabel.Text = string.Empty;
        RefreshLists();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        HeaderText.FontFamily = font;
        HeaderText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        SearchBox.FontFamily = font;
        StatusLabel.FontFamily = font;
        StatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);

        StyleActionButton(SearchButton, theme, accent: true);
        StyleActionButton(AddButton, theme);
        RefreshLists();
    }

    private static void StyleActionButton(Button button, ThemeDefinition theme, bool accent = false)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = accent
            ? ThemePainter.Brush(theme.Accent, 0.85)
            : ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Accent, 0.55);
        button.BorderThickness = new Thickness(1);
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            ApplySearch();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Live filter for short queries; keep light (registered items only).
        if ((SearchBox.Text?.Length ?? 0) <= 40)
        {
            ApplySearch();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        _filter = SearchBox.Text?.Trim() ?? string.Empty;
        if (_commands is null)
        {
            return;
        }

        var result = _commands.Execute(CreativeCommand.SearchItems(_filter));
        if (!result.Succeeded)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Search failed.";
            return;
        }

        StatusLabel.Text = string.IsNullOrEmpty(_filter)
            ? string.Empty
            : $"{result.Items.Count} match(es)";
        RebuildContent(result.Items, searching: !string.IsNullOrEmpty(_filter));
    }

    private void RefreshLists()
    {
        if (_commands is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_filter))
        {
            ApplySearch();
            return;
        }

        RebuildContent(_commands.Workspace.Items, searching: false);
    }

    private void RebuildContent(IReadOnlyList<CreativeItem> allOrFiltered, bool searching)
    {
        ContentList.Children.Clear();
        if (_commands is null)
        {
            return;
        }

        if (searching)
        {
            ContentList.Children.Add(CreateSectionHeader("Results"));
            if (allOrFiltered.Count == 0)
            {
                ContentList.Children.Add(CreateMuted("No matches in your workspace."));
            }
            else
            {
                foreach (var item in allOrFiltered)
                {
                    ContentList.Children.Add(CreateItemRow(item));
                }
            }

            return;
        }

        var favorites = _commands.Workspace.GetFavorites();
        ContentList.Children.Add(CreateSectionHeader("Favorites"));
        if (favorites.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Star items you use often."));
        }
        else
        {
            foreach (var item in favorites)
            {
                ContentList.Children.Add(CreateItemRow(item));
            }
        }

        var recent = _commands.Workspace.GetRecent();
        ContentList.Children.Add(CreateSectionHeader("Recent"));
        if (recent.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Open something to see it here."));
        }
        else
        {
            foreach (var item in recent)
            {
                ContentList.Children.Add(CreateItemRow(item));
            }
        }

        var rest = allOrFiltered
            .Where(i => !favorites.Any(f => f.Id == i.Id) && !recent.Any(r => r.Id == i.Id))
            .ToList();
        if (rest.Count > 0)
        {
            ContentList.Children.Add(CreateSectionHeader("All"));
            foreach (var item in rest)
            {
                ContentList.Children.Add(CreateItemRow(item));
            }
        }
        else if (favorites.Count == 0 && recent.Count == 0 && allOrFiltered.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Your desk is empty. Add a file or folder."));
        }
    }

    private UIElement CreateSectionHeader(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 6, 0, 2),
            Opacity = 0.9
        };
        if (_theme is not null)
        {
            block.FontFamily = new FontFamily(_theme.FontFamily);
            block.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
        }

        return block;
    }

    private UIElement CreateItemRow(CreativeItem item)
    {
        var open = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Tag = item.Id
        };
        open.Click += ItemOpen_Click;
        open.DoubleTapped += ItemOpen_DoubleTapped;

        var title = $"{TypeGlyph(item.ItemType)}  {item.Name}";
        if (item.IsFavorite)
        {
            title = "★ " + title;
        }

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{item.ItemType} · {item.Path}",
            FontSize = 10,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        open.Content = panel;

        var fav = new Button
        {
            Content = item.IsFavorite ? "★" : "☆",
            MinWidth = 36,
            Tag = item.Id,
            ToolTipService.ToolTip = "Toggle favorite"
        };
        fav.Click += Favorite_Click;

        if (_theme is not null)
        {
            StyleActionButton(open, _theme);
            StyleActionButton(fav, _theme);
            if (open.Content is StackPanel sp)
            {
                foreach (var child in sp.Children.OfType<TextBlock>())
                {
                    child.FontFamily = new FontFamily(_theme.FontFamily);
                    child.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
                }
            }
        }

        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(open, 0);
        Grid.SetColumn(fav, 1);
        row.Children.Add(open);
        row.Children.Add(fav);
        return row;
    }

    private static string TypeGlyph(CreativeItemType type) =>
        type switch
        {
            CreativeItemType.Folder => "📁",
            CreativeItemType.Project => "💻",
            _ => "📄"
        };

    private void ItemOpen_Click(object sender, RoutedEventArgs e)
    {
        // Single click opens (desk metaphor — one click to return to work).
        if (sender is Button { Tag: string id })
        {
            OpenRegistered(id);
        }
    }

    private void ItemOpen_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: string id })
        {
            OpenRegistered(id);
        }
    }

    private void OpenRegistered(string id)
    {
        if (_commands is null || _tryLaunch is null)
        {
            return;
        }

        var result = _commands.Execute(CreativeCommand.OpenItem(id));
        if (!result.Succeeded || result.Item is null)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Open failed.";
            return;
        }

        if (!result.ShouldLaunch)
        {
            return;
        }

        var launchError = _tryLaunch(result.Item);
        if (launchError is null)
        {
            StatusLabel.Text = $"Opened {result.Item.Name}";
            RefreshLists();
        }
        else
        {
            StatusLabel.Text = launchError;
            RefreshLists();
        }
    }

    private void Favorite_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || sender is not Button { Tag: string id })
        {
            return;
        }

        var result = _commands.Execute(CreativeCommand.ToggleFavorite(id));
        if (!result.Succeeded)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Favorite failed.";
            return;
        }

        RefreshLists();
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || _pickFile is null || _pickFolder is null)
        {
            return;
        }

        var asProject = new CheckBox
        {
            Content = "Register folder as Project",
            IsChecked = _configuration.AddFoldersAsProjects
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Add a file or folder you use often. Opening uses Windows defaults. No delete/move.",
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(asProject);

        var dialog = new ContentDialog
        {
            Title = "Add to Creative Workspace",
            Content = panel,
            PrimaryButtonText = "Add File",
            SecondaryButtonText = "Add Folder",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var choice = await dialog.ShowAsync();
        if (choice == ContentDialogResult.None)
        {
            return;
        }

        _configuration.AddFoldersAsProjects = asProject.IsChecked == true;
        _onConfigurationChanged?.Invoke(_configuration);

        if (choice == ContentDialogResult.Primary)
        {
            var picked = await _pickFile();
            if (picked.cancelled)
            {
                return;
            }

            if (!picked.ok || string.IsNullOrWhiteSpace(picked.path))
            {
                StatusLabel.Text = picked.error ?? "File pick failed.";
                return;
            }

            if (!_commands.Workspace.TryAdd(picked.path!, CreativeItemType.File, null, out var item, out var error))
            {
                StatusLabel.Text = error ?? "Could not add file.";
                return;
            }

            StatusLabel.Text = $"Added {item!.Name}";
            RefreshLists();
            return;
        }

        if (choice == ContentDialogResult.Secondary)
        {
            var picked = await _pickFolder();
            if (picked.cancelled)
            {
                return;
            }

            if (!picked.ok || string.IsNullOrWhiteSpace(picked.path))
            {
                StatusLabel.Text = picked.error ?? "Folder pick failed.";
                return;
            }

            var type = _configuration.AddFoldersAsProjects
                ? CreativeItemType.Project
                : CreativeItemType.Folder;
            if (!_commands.Workspace.TryAdd(picked.path!, type, null, out var item, out var error))
            {
                StatusLabel.Text = error ?? "Could not add folder.";
                return;
            }

            StatusLabel.Text = $"Added {item!.Name}";
            RefreshLists();
        }
    }

    private TextBlock CreateMuted(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 2, 0, 6)
        };
        if (_theme is not null)
        {
            block.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            block.FontFamily = new FontFamily(_theme.FontFamily);
        }

        return block;
    }
}
