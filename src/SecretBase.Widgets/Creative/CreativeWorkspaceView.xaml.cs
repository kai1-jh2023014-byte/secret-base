using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Ai;
using SecretBase.Core.Creative;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Creative;
using SecretBase.Core.Widgets.Web;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Creative;

/// <summary>
/// Creative Workspace UI. Opens only registered items/projects via host callbacks.
/// Delete Project removes registration only. No FS delete/move/rename. No Host Bridge.
/// </summary>
public sealed partial class CreativeWorkspaceView : UserControl
{
    private CreativeWorkspaceWidgetConfiguration _configuration =
        CreativeWorkspaceWidgetConfiguration.CreateDefault();
    private CreativeCommandService? _commands;
    private Func<CreativeItem, string?>? _tryLaunchItem;
    private Func<string, bool, string?>? _tryLaunchTarget;
    private Func<string?, bool, string?>? _tryLaunchCursor;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFile;
    private Func<Task<(bool ok, bool cancelled, string? path, string? error)>>? _pickFolder;
    private Action<CreativeWorkspaceWidgetConfiguration>? _onConfigurationChanged;
    private ThemeDefinition? _theme;
    private string _filter = string.Empty;
    private string? _detailProjectId;

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
        Action<CreativeWorkspaceWidgetConfiguration>? onConfigurationChanged = null,
        Func<string, bool, string?>? tryLaunchTarget = null,
        Func<string?, bool, string?>? tryLaunchCursor = null)
    {
        _commands = commands;
        _configuration = configuration;
        _tryLaunchItem = tryLaunch;
        _tryLaunchTarget = tryLaunchTarget;
        _tryLaunchCursor = tryLaunchCursor;
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
        StyleActionButton(NewProjectButton, theme, accent: true);
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
        if ((SearchBox.Text?.Length ?? 0) <= 40)
        {
            ApplySearch();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => ApplySearch();

    private void ApplySearch()
    {
        _filter = SearchBox.Text?.Trim() ?? string.Empty;
        _detailProjectId = null;
        if (_commands is null)
        {
            return;
        }

        var itemResult = _commands.Execute(CreativeCommand.SearchItems(_filter));
        if (!itemResult.Succeeded)
        {
            StatusLabel.Text = itemResult.ErrorMessage ?? "Search failed.";
            return;
        }

        IReadOnlyList<CreativeProject> projects = Array.Empty<CreativeProject>();
        if (_commands.Projects is not null)
        {
            var projectResult = _commands.Execute(CreativeCommand.SearchProjects(_filter));
            if (projectResult.Succeeded)
            {
                projects = projectResult.Projects;
            }
        }

        StatusLabel.Text = string.IsNullOrEmpty(_filter)
            ? string.Empty
            : $"{projects.Count + itemResult.Items.Count} match(es)";
        RebuildContent(itemResult.Items, projects, searching: !string.IsNullOrEmpty(_filter));
    }

    private void RefreshLists()
    {
        if (_commands is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_detailProjectId))
        {
            ShowProjectDashboard(_detailProjectId);
            return;
        }

        if (!string.IsNullOrEmpty(_filter))
        {
            ApplySearch();
            return;
        }

        var projects = _commands.Projects?.Projects ?? (IReadOnlyList<CreativeProject>)Array.Empty<CreativeProject>();
        RebuildContent(_commands.Workspace.Items, projects, searching: false);
    }

    private void RebuildContent(
        IReadOnlyList<CreativeItem> allOrFiltered,
        IReadOnlyList<CreativeProject> projects,
        bool searching)
    {
        ContentList.Children.Clear();
        if (_commands is null)
        {
            return;
        }

        HeaderText.Text = "Creative Workspace";

        if (searching)
        {
            ContentList.Children.Add(CreateSectionHeader("Projects"));
            if (projects.Count == 0)
            {
                ContentList.Children.Add(CreateMuted("No matching projects."));
            }
            else
            {
                foreach (var project in projects)
                {
                    ContentList.Children.Add(CreateProjectRow(project));
                }
            }

            ContentList.Children.Add(CreateSectionHeader("Files & Folders"));
            if (allOrFiltered.Count == 0)
            {
                ContentList.Children.Add(CreateMuted("No matching files or folders."));
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

        ContentList.Children.Add(CreateSectionHeader("★ Projects"));
        if (projects.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Create a Project to gather files, folders, and links."));
        }
        else
        {
            foreach (var project in projects
                         .OrderByDescending(p => p.IsFavorite)
                         .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
            {
                ContentList.Children.Add(CreateProjectRow(project));
            }
        }

        var favorites = _commands.Workspace.GetFavorites();
        ContentList.Children.Add(CreateSectionHeader("Favorites"));
        if (favorites.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Star files/folders you use often."));
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
            ContentList.Children.Add(CreateSectionHeader("Files & Folders"));
            foreach (var item in rest)
            {
                ContentList.Children.Add(CreateItemRow(item));
            }
        }
        else if (favorites.Count == 0 && recent.Count == 0 && allOrFiltered.Count == 0 && projects.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Your desk is empty. Create a Project or add a file."));
        }
    }

    private void ShowProjectDashboard(string projectId)
    {
        ContentList.Children.Clear();
        if (_commands?.Projects is null)
        {
            _detailProjectId = null;
            RefreshLists();
            return;
        }

        var project = _commands.Projects.FindById(projectId);
        if (project is null)
        {
            _detailProjectId = null;
            StatusLabel.Text = "Project not found.";
            RefreshLists();
            return;
        }

        _detailProjectId = project.Id;

        HeaderText.Text = $"{ProjectTypeGlyph(project.ProjectType)} {project.Name}";

        var headerRow = new Grid { ColumnSpacing = 6, Margin = new Thickness(0, 0, 0, 4) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var typeLabel = CreateMuted(ProjectTypeLabel(project.ProjectType));
        var favButton = new Button
        {
            Content = project.IsFavorite ? "★" : "☆",
            MinWidth = 36,
            ToolTipService.ToolTip = "Favorite"
        };
        favButton.Click += (_, _) =>
        {
            var result = _commands.Execute(CreativeCommand.ToggleCreativeProjectFavorite(project.Id));
            StatusLabel.Text = result.Succeeded
                ? (result.Project!.IsFavorite ? "Favorited" : "Unfavorited")
                : (result.ErrorMessage ?? "Favorite failed.");
            RefreshLists();
        };
        if (_theme is not null)
        {
            StyleActionButton(favButton, _theme, accent: project.IsFavorite);
        }

        Grid.SetColumn(typeLabel, 0);
        Grid.SetColumn(favButton, 1);
        headerRow.Children.Add(typeLabel);
        headerRow.Children.Add(favButton);
        ContentList.Children.Add(headerRow);

        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            var desc = new TextBlock
            {
                Text = project.Description,
                FontSize = 12,
                TextWrapping = TextWrapping.WrapWholeWords,
                Margin = new Thickness(0, 2, 0, 8)
            };
            if (_theme is not null)
            {
                desc.FontFamily = new FontFamily(_theme.FontFamily);
                desc.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
            }

            ContentList.Children.Add(desc);
        }

        // Quick Actions — user-explicit resources + Root + AI tools.
        ContentList.Children.Add(CreateSectionHeader("Quick Actions"));
        var quickPanel = new StackPanel { Spacing = 6, Orientation = Orientation.Vertical };
        var quickResources = project.Resources.Where(r => r.IsQuickAction).ToList();
        if (!string.IsNullOrWhiteSpace(project.RootFolder))
        {
            quickPanel.Children.Add(CreateActionButton(
                "📁 Root Folder",
                () =>
                {
                    LaunchCommandResult(_commands.Execute(CreativeCommand.OpenCreativeProjectRoot(project.Id)));
                    return Task.CompletedTask;
                }));
        }

        quickPanel.Children.Add(CreateActionButton(
            "💻 Open in Cursor",
            async () =>
            {
                var result = _commands.Execute(CreativeCommand.OpenProjectInCursor(project.Id));
                if (!result.Succeeded && result.OfferCursorWebsiteFallback)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Cursor is not available",
                        Content = "Cursor was not found on this PC. Open the official Cursor website?",
                        PrimaryButtonText = "Open Cursor Website",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary
                        && _tryLaunchTarget is not null
                        && WebUrlValidator.TryNormalize(AiBuiltinTools.CursorWebsite, out var url, out _)
                        && url is not null)
                    {
                        StatusLabel.Text = _tryLaunchTarget(url, true) ?? "Opened Cursor website.";
                    }
                    else
                    {
                        StatusLabel.Text = result.ErrorMessage ?? "Cursor is not available.";
                    }

                    RefreshLists();
                    return;
                }

                LaunchCommandResult(result);
            }));
        quickPanel.Children.Add(CreateActionButton(
            "🌐 Open ChatGPT",
            () =>
            {
                LaunchCommandResult(_commands.Execute(CreativeCommand.OpenAiTool(AiBuiltinTools.ChatGptId)));
                return Task.CompletedTask;
            }));

        foreach (var resource in quickResources)
        {
            var captured = resource;
            quickPanel.Children.Add(CreateActionButton(
                $"{ResourceGlyph(captured.Kind)} {captured.Name}",
                () =>
                {
                    LaunchCommandResult(
                        _commands.Execute(CreativeCommand.OpenCreativeProjectResource(project.Id, captured.Id)));
                    return Task.CompletedTask;
                }));
        }

        ContentList.Children.Add(quickPanel);

        // Recent — Secret Base opens only.
        ContentList.Children.Add(CreateSectionHeader("Recent"));
        if (project.RecentItems.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("Open something from this Project to see it here."));
        }
        else
        {
            foreach (var recent in project.RecentItems)
            {
                ContentList.Children.Add(CreateRecentRow(project.Id, recent));
            }
        }

        // Resources grouped.
        var files = project.Resources.Where(r => r.Kind == CreativeProjectResourceKind.File).ToList();
        var folders = project.Resources.Where(r => r.Kind == CreativeProjectResourceKind.Folder).ToList();
        var links = project.Resources.Where(r => r.Kind == CreativeProjectResourceKind.ExternalLink).ToList();

        ContentList.Children.Add(CreateSectionHeader("Resources"));
        ContentList.Children.Add(CreateSectionHeader("Files"));
        if (files.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("No files registered."));
        }
        else
        {
            foreach (var resource in files)
            {
                ContentList.Children.Add(CreateProjectResourceRow(project.Id, resource));
            }
        }

        ContentList.Children.Add(CreateSectionHeader("Folders"));
        if (folders.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("No folders registered."));
        }
        else
        {
            foreach (var resource in folders)
            {
                ContentList.Children.Add(CreateProjectResourceRow(project.Id, resource));
            }
        }

        ContentList.Children.Add(CreateSectionHeader("Links"));
        if (links.Count == 0)
        {
            ContentList.Children.Add(CreateMuted("No links registered."));
        }
        else
        {
            foreach (var resource in links)
            {
                ContentList.Children.Add(CreateProjectResourceRow(project.Id, resource));
            }
        }

        // Notes
        ContentList.Children.Add(CreateSectionHeader("Notes"));
        var notesBox = new TextBox
        {
            Text = project.Notes ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 88,
            PlaceholderText = "What are you working on?"
        };
        if (_theme is not null)
        {
            notesBox.FontFamily = new FontFamily(_theme.FontFamily);
        }

        ContentList.Children.Add(notesBox);
        ContentList.Children.Add(CreateActionButton("Save Notes", () =>
        {
            var result = _commands.Execute(CreativeCommand.SaveCreativeProjectNotes(project.Id, notesBox.Text));
            StatusLabel.Text = result.Succeeded ? "Notes saved." : (result.ErrorMessage ?? "Save failed.");
            if (result.Succeeded)
            {
                RefreshLists();
            }

            return Task.CompletedTask;
        }));

        var actions = new StackPanel { Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(CreateActionButton("Edit Project", () => ShowEditProjectDialogAsync(project)));
        actions.Children.Add(CreateActionButton("+ Add File", () => AddResourceAsync(project.Id, CreativeProjectResourceKind.File)));
        actions.Children.Add(CreateActionButton("+ Add Folder", () => AddResourceAsync(project.Id, CreativeProjectResourceKind.Folder)));
        actions.Children.Add(CreateActionButton("+ Add Link", () => AddLinkAsync(project.Id)));
        actions.Children.Add(CreateActionButton("Remove from Secret Base", () => DeleteProjectRegistrationAsync(project)));
        actions.Children.Add(CreateActionButton("← Back", () =>
        {
            _detailProjectId = null;
            RefreshLists();
            return Task.CompletedTask;
        }));
        ContentList.Children.Add(actions);
    }

    private UIElement CreateRecentRow(string projectId, CreativeProjectRecentItem recent)
    {
        var button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 6, 8, 6),
            Content = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{(recent.IsRoot ? "📁" : ResourceGlyph(recent.Kind))}  {recent.Name}",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = recent.Target,
                        FontSize = 10,
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            }
        };
        button.Click += (_, _) =>
        {
            if (_commands is null)
            {
                return;
            }

            if (recent.IsRoot)
            {
                LaunchCommandResult(_commands.Execute(CreativeCommand.OpenCreativeProjectRoot(projectId)));
            }
            else
            {
                LaunchCommandResult(
                    _commands.Execute(CreativeCommand.OpenCreativeProjectResource(projectId, recent.Key)));
            }
        };
        if (_theme is not null)
        {
            StyleActionButton(button, _theme);
            ThemeTextInButton(button);
        }

        return button;
    }

    private UIElement CreateProjectRow(CreativeProject project)
    {
        var open = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 8, 10, 8),
            Tag = project.Id
        };
        open.Click += ProjectOpen_Click;

        var title = $"{ProjectTypeGlyph(project.ProjectType)}  {project.Name}";
        if (project.IsFavorite)
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
            Text = string.IsNullOrWhiteSpace(project.Description)
                ? ProjectTypeLabel(project.ProjectType)
                : $"{ProjectTypeLabel(project.ProjectType)} · {project.Description}",
            FontSize = 10,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        open.Content = panel;

        var fav = new Button
        {
            Content = project.IsFavorite ? "★" : "☆",
            MinWidth = 36,
            Tag = project.Id,
            ToolTipService.ToolTip = "Toggle favorite"
        };
        fav.Click += ProjectFavorite_Click;

        if (_theme is not null)
        {
            StyleActionButton(open, _theme);
            StyleActionButton(fav, _theme);
            ThemeTextInButton(open);
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

    private UIElement CreateProjectResourceRow(string projectId, CreativeProjectResource resource)
    {
        var open = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 6, 8, 6),
            Content = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = $"{ResourceGlyph(resource.Kind)}  {resource.Name}",
                        FontSize = 12,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = resource.Target,
                        FontSize = 10,
                        Opacity = 0.75,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            }
        };
        open.Click += (_, _) =>
        {
            if (_commands is null)
            {
                return;
            }

            LaunchCommandResult(
                _commands.Execute(CreativeCommand.OpenCreativeProjectResource(projectId, resource.Id)));
        };

        var star = new Button
        {
            Content = resource.IsQuickAction ? "★" : "☆",
            MinWidth = 32,
            ToolTipService.ToolTip = "Pin as Quick Action"
        };
        star.Click += (_, _) =>
        {
            if (_commands is null)
            {
                return;
            }

            var wasPinned = resource.IsQuickAction;
            var result = _commands.Execute(
                CreativeCommand.ToggleCreativeProjectResourceQuickAction(projectId, resource.Id));
            StatusLabel.Text = result.Succeeded
                ? (wasPinned ? "Removed from Quick Actions." : "Pinned to Quick Actions.")
                : (result.ErrorMessage ?? "Toggle failed.");
            RefreshLists();
        };

        var remove = new Button
        {
            Content = "×",
            MinWidth = 32,
            ToolTipService.ToolTip = "Remove registration only"
        };
        remove.Click += (_, _) =>
        {
            if (_commands?.Projects is null)
            {
                return;
            }

            if (_commands.Projects.TryRemoveResource(projectId, resource.Id, out _, out var error))
            {
                StatusLabel.Text = "Removed resource registration.";
                RefreshLists();
            }
            else
            {
                StatusLabel.Text = error ?? "Remove failed.";
            }
        };

        if (_theme is not null)
        {
            StyleActionButton(open, _theme);
            StyleActionButton(star, _theme, accent: resource.IsQuickAction);
            StyleActionButton(remove, _theme);
            ThemeTextInButton(open);
        }

        var row = new Grid { ColumnSpacing = 6 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(open, 0);
        Grid.SetColumn(star, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(open);
        row.Children.Add(star);
        row.Children.Add(remove);
        return row;
    }

    private Button CreateActionButton(string label, Func<Task> onClick)
    {
        var button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 6, 10, 6)
        };
        button.Click += async (_, _) => await onClick();
        if (_theme is not null)
        {
            StyleActionButton(button, _theme);
        }

        return button;
    }

    private void ThemeTextInButton(Button button)
    {
        if (_theme is null || button.Content is not StackPanel sp)
        {
            return;
        }

        foreach (var child in sp.Children.OfType<TextBlock>())
        {
            child.FontFamily = new FontFamily(_theme.FontFamily);
            child.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
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
            ThemeTextInButton(open);
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

    private static string ProjectTypeGlyph(CreativeProjectType type) =>
        type switch
        {
            CreativeProjectType.Music => "🎵",
            CreativeProjectType.Programming => "💻",
            CreativeProjectType.Video => "🎬",
            CreativeProjectType.Design => "🎨",
            CreativeProjectType.Writing => "✍",
            _ => "📦"
        };

    private static string ProjectTypeLabel(CreativeProjectType type) =>
        type switch
        {
            CreativeProjectType.Music => "Music Project",
            CreativeProjectType.Programming => "Programming Project",
            CreativeProjectType.Video => "Video Project",
            CreativeProjectType.Design => "Design Project",
            CreativeProjectType.Writing => "Writing Project",
            _ => "Creative Project"
        };

    private static string ResourceGlyph(CreativeProjectResourceKind kind) =>
        kind switch
        {
            CreativeProjectResourceKind.Folder => "📁",
            CreativeProjectResourceKind.ExternalLink => "🔗",
            _ => "📄"
        };

    private void ProjectOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && _commands is not null)
        {
            _ = _commands.Execute(CreativeCommand.OpenCreativeProject(id));
            _detailProjectId = id;
            RefreshLists();
        }
    }

    private void ProjectFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || sender is not Button { Tag: string id })
        {
            return;
        }

        var result = _commands.Execute(CreativeCommand.ToggleCreativeProjectFavorite(id));
        if (!result.Succeeded)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Favorite failed.";
            return;
        }

        RefreshLists();
    }

    private void ItemOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            OpenRegisteredItem(id);
        }
    }

    private void ItemOpen_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: string id })
        {
            OpenRegisteredItem(id);
        }
    }

    private void OpenRegisteredItem(string id)
    {
        if (_commands is null || _tryLaunchItem is null)
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

        var launchError = _tryLaunchItem(result.Item);
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

    private void LaunchCommandResult(CreativeCommandResult result)
    {
        if (!result.Succeeded)
        {
            if (result.OfferCursorWebsiteFallback && !string.IsNullOrWhiteSpace(result.LaunchTarget)
                && _tryLaunchTarget is not null)
            {
                StatusLabel.Text = result.ErrorMessage ?? "Cursor is not available.";
                // Host may open website via fallback button flow; keep message only here.
                RefreshLists();
                return;
            }

            StatusLabel.Text = result.ErrorMessage ?? "Open failed.";
            RefreshLists();
            return;
        }

        if (result.ShouldOpenCursorAtFolder || result.ShouldOpenCursorApp)
        {
            if (_tryLaunchCursor is null)
            {
                StatusLabel.Text = "Cursor launch is unavailable.";
                return;
            }

            var cursorError = _tryLaunchCursor(
                result.ShouldOpenCursorAtFolder ? result.CursorFolderPath : null,
                openAppOnly: result.ShouldOpenCursorApp && !result.ShouldOpenCursorAtFolder);
            StatusLabel.Text = cursorError ?? "Opened in Cursor.";
            RefreshLists();
            return;
        }

        if (!result.ShouldLaunch || string.IsNullOrWhiteSpace(result.LaunchTarget))
        {
            StatusLabel.Text = result.Project is null
                ? "Opened."
                : $"Opened {result.Project.Name} (no root to launch).";
            RefreshLists();
            return;
        }

        if (_tryLaunchTarget is null)
        {
            StatusLabel.Text = "Launch service unavailable.";
            return;
        }

        var error = _tryLaunchTarget(result.LaunchTarget!, result.LaunchIsExternalLink);
        StatusLabel.Text = error is null
            ? (result.Resource?.Name is { } name ? $"Opened {name}" : "Opened.")
            : error;
        RefreshLists();
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

    private async void NewProjectButton_Click(object sender, RoutedEventArgs e) =>
        await ShowCreateProjectDialogAsync();

    private async Task ShowCreateProjectDialogAsync()
    {
        if (_commands?.Projects is null || _pickFolder is null)
        {
            StatusLabel.Text = "Projects are not available.";
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "My New Project", MinHeight = 32 };
        var descBox = new TextBox
        {
            PlaceholderText = "Description (optional)",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64
        };
        var rootBox = new TextBox { PlaceholderText = @"D:\Projects\MyProject", MinHeight = 32 };
        var typeBox = CreateProjectTypeCombo(CreativeProjectType.Other);
        var browse = new Button { Content = "Browse", MinWidth = 72 };
        browse.Click += async (_, _) =>
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
        Grid.SetColumn(browse, 1);
        rootRow.Children.Add(rootBox);
        rootRow.Children.Add(browse);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Labeled("Name", nameBox));
        panel.Children.Add(Labeled("Description", descBox));
        panel.Children.Add(Labeled("Root Folder", rootRow));
        panel.Children.Add(Labeled("Type", typeBox));

        var dialog = new ContentDialog
        {
            Title = "Create Project",
            Content = panel,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var type = typeBox.SelectedItem is CreativeProjectType t ? t : CreativeProjectType.Other;
        if (!_commands.Projects.TryCreate(nameBox.Text, descBox.Text, type, rootBox.Text, out var project, out var error))
        {
            StatusLabel.Text = error ?? "Could not create project.";
            return;
        }

        StatusLabel.Text = $"Created {project!.Name}";
        _detailProjectId = project.Id;
        RefreshLists();
    }

    private async Task ShowEditProjectDialogAsync(CreativeProject project)
    {
        if (_commands?.Projects is null || _pickFolder is null)
        {
            return;
        }

        var nameBox = new TextBox { Text = project.Name, MinHeight = 32 };
        var descBox = new TextBox
        {
            Text = project.Description ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64
        };
        var rootBox = new TextBox { Text = project.RootFolder ?? string.Empty, MinHeight = 32 };
        var typeBox = CreateProjectTypeCombo(project.ProjectType);
        var browse = new Button { Content = "Browse", MinWidth = 72 };
        browse.Click += async (_, _) =>
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
        Grid.SetColumn(browse, 1);
        rootRow.Children.Add(rootBox);
        rootRow.Children.Add(browse);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Labeled("Name", nameBox));
        panel.Children.Add(Labeled("Description", descBox));
        panel.Children.Add(Labeled("Root Folder", rootRow));
        panel.Children.Add(Labeled("Type", typeBox));

        var dialog = new ContentDialog
        {
            Title = "Edit Project",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var type = typeBox.SelectedItem is CreativeProjectType t ? t : CreativeProjectType.Other;
        if (!_commands.Projects.TryUpdate(
                project.Id, nameBox.Text, descBox.Text, type, rootBox.Text, out var updated, out var error))
        {
            StatusLabel.Text = error ?? "Could not update project.";
            return;
        }

        StatusLabel.Text = $"Updated {updated!.Name}";
        RefreshLists();
    }

    private async Task AddResourceAsync(string projectId, CreativeProjectResourceKind kind)
    {
        if (_commands?.Projects is null || _pickFile is null || _pickFolder is null)
        {
            return;
        }

        var picked = kind == CreativeProjectResourceKind.Folder
            ? await _pickFolder()
            : await _pickFile();
        if (picked.cancelled)
        {
            return;
        }

        if (!picked.ok || string.IsNullOrWhiteSpace(picked.path))
        {
            StatusLabel.Text = picked.error ?? "Pick failed.";
            return;
        }

        var name = Path.GetFileName(picked.path!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name))
        {
            name = kind == CreativeProjectResourceKind.Folder ? "Folder" : "File";
        }

        if (!_commands.Projects.TryAddResource(projectId, kind, name, picked.path!, out _, out var error))
        {
            StatusLabel.Text = error ?? "Could not add resource.";
            return;
        }

        StatusLabel.Text = $"Added {name}";
        RefreshLists();
    }

    private async Task AddLinkAsync(string projectId)
    {
        if (_commands?.Projects is null)
        {
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "YouTube", MinHeight = 32 };
        var urlBox = new TextBox { PlaceholderText = "https://…", MinHeight = 32 };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Labeled("Name", nameBox));
        panel.Children.Add(Labeled("URL (https)", urlBox));

        var dialog = new ContentDialog
        {
            Title = "Add External Link",
            Content = panel,
            PrimaryButtonText = "Add",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (!WebUrlValidator.TryNormalize(urlBox.Text, out _, out var urlError))
        {
            StatusLabel.Text = urlError ?? "Invalid URL.";
            return;
        }

        if (!_commands.Projects.TryAddResource(
                projectId,
                CreativeProjectResourceKind.ExternalLink,
                nameBox.Text,
                urlBox.Text,
                out _,
                out var error))
        {
            StatusLabel.Text = error ?? "Could not add link.";
            return;
        }

        StatusLabel.Text = "Added link.";
        RefreshLists();
    }

    private async Task DeleteProjectRegistrationAsync(CreativeProject project)
    {
        if (_commands is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Remove Project?",
            Content =
                $"Remove “{project.Name}” from Secret Base only?\n\nWindows files and folders will not be deleted.",
            PrimaryButtonText = "Remove registration",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var result = _commands.Execute(CreativeCommand.DeleteCreativeProjectRegistration(project.Id));
        if (!result.Succeeded)
        {
            StatusLabel.Text = result.ErrorMessage ?? "Remove failed.";
            return;
        }

        StatusLabel.Text = "Project registration removed.";
        _detailProjectId = null;
        RefreshLists();
    }

    private static ComboBox CreateProjectTypeCombo(CreativeProjectType selected)
    {
        var box = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 32
        };
        foreach (CreativeProjectType type in Enum.GetValues<CreativeProjectType>())
        {
            box.Items.Add(type);
        }

        box.SelectedItem = selected;
        return box;
    }

    private static UIElement Labeled(string label, UIElement control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Opacity = 0.85 });
        panel.Children.Add(control);
        return panel;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || _pickFile is null || _pickFolder is null)
        {
            return;
        }

        var asProject = new CheckBox
        {
            Content = "Register folder as Project (legacy folder mark)",
            IsChecked = _configuration.AddFoldersAsProjects
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = "Add a file or folder shortcut. Prefer + Project for creative containers. Opening uses Windows defaults. No delete/move.",
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
