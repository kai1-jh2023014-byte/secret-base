using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SecretBase.Core.Capture;
using SecretBase.Core.Commands;
using SecretBase.Core.Connectors;
using SecretBase.Core.Memory;

namespace SecretBase.App;

public sealed partial class DesktopPage
{
    private IReadOnlyList<PaletteItem> _paletteItems = [];

    private void PaletteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        ShowPaletteOverlay();
    }

    private void ShowPaletteOverlay()
    {
        if (_baseExperience is null)
        {
            return;
        }

        AllowFullWindowInput();
        PaletteOverlay.Visibility = Visibility.Visible;
        PaletteQueryBox.Text = string.Empty;
        RefreshPaletteItems(string.Empty);
        PaletteQueryBox.Focus(FocusState.Programmatic);
    }

    private void HidePaletteOverlay()
    {
        PaletteOverlay.Visibility = Visibility.Collapsed;
    }

    private void PaletteOverlay_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (e.OriginalSource == PaletteOverlay)
        {
            HidePaletteOverlay();
        }
    }

    private void PaletteQueryBox_TextChanged(object sender, TextChangedEventArgs e) =>
        RefreshPaletteItems(PaletteQueryBox.Text);

    private async void PaletteQueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            HidePaletteOverlay();
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            var selected = SelectedPaletteItem();
            if (selected is not null)
            {
                await RunPaletteItemAsync(selected);
            }
        }
    }

    private async void PaletteList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var selected = SelectedPaletteItem();
        if (selected is not null)
        {
            await RunPaletteItemAsync(selected);
        }
    }

    private PaletteItem? SelectedPaletteItem()
    {
        var index = PaletteList.SelectedIndex;
        if (index >= 0 && index < _paletteItems.Count)
        {
            return _paletteItems[index];
        }

        return _paletteItems.FirstOrDefault();
    }

    private void RefreshPaletteItems(string? query)
    {
        if (_baseExperience is null)
        {
            return;
        }

        _paletteItems = _baseExperience.Palette(query ?? string.Empty);
        PaletteList.ItemsSource = _paletteItems
            .Select(item => item.Title + "  —  " + item.Subtitle)
            .ToList();
        PaletteList.Tag = _paletteItems;
        if (_paletteItems.Count > 0)
        {
            PaletteList.SelectedIndex = 0;
        }
    }

    private async Task RunPaletteItemAsync(PaletteItem item)
    {
        HidePaletteOverlay();
        if (_baseExperience is null)
        {
            return;
        }

        _baseExperience.Activity.Record(new SecretBase.Core.Activity.ActivityEvent
        {
            Kind = SecretBase.Core.Activity.ActivityKind.CommandExecuted,
            Title = item.Title,
            At = _baseExperience.Now,
            Source = "palette"
        });

        if (item.Action is "open-project" or "open-app")
        {
            ShowHostStatus(item.Title + " still requires confirmation in Base AI.");
            return;
        }

        if (item.Action == "capture")
        {
            await ShowQuickCaptureDialogAsync();
            return;
        }

        if (item.Action == "memory")
        {
            await ShowMemoryDialogAsync();
            return;
        }

        if (item.Action == "integrations")
        {
            await ShowIntegrationsDialogAsync();
            return;
        }

        var dispatch = _baseExperience.Dispatch(PersonalSpaceCatalog.UtteranceFor(item));
        if (dispatch.Kind == CommandKind.Focus)
        {
            _ = _assistant?.SendAsync(PersonalSpaceCatalog.UtteranceFor(item));
            ShowHostStatus(dispatch.Body);
            return;
        }

        if (dispatch.Kind == CommandKind.Continue)
        {
            await ShowTextDialogAsync(dispatch.Title, dispatch.Body, "Continue", async () =>
            {
                if (_baseExperience.CurrentWorkspace is { } session)
                {
                    await ContinueWorkspaceAsync(session);
                }
            });
            return;
        }

        if (dispatch.Kind == CommandKind.Privacy)
        {
            await ShowPrivacyDialogAsync();
            return;
        }

        if (dispatch.Kind == CommandKind.Integrations)
        {
            await ShowIntegrationsDialogAsync();
            return;
        }

        await ShowTextDialogAsync(dispatch.Title, dispatch.Body);
    }

    private async Task ShowQuickCaptureDialogAsync()
    {
        if (_baseExperience is null)
        {
            return;
        }

        AllowFullWindowInput();
        var box = new TextBox
        {
            Header = "Capture",
            PlaceholderText = "An idea, todo, or note…",
            AcceptsReturn = true,
            Height = 96
        };
        var destinations = new ComboBox
        {
            Header = "Save as",
            ItemsSource = new[] { "Auto", "Idea", "Todo", "Note", "Memory", "Project" },
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(box);
        panel.Children.Add(destinations);
        var dialog = new ContentDialog
        {
            Title = "Quick capture",
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

        var draft = _baseExperience.ClassifyCapture(box.Text ?? string.Empty);
        CaptureDestination? force = destinations.SelectedItem as string switch
        {
            "Idea" => CaptureDestination.Idea,
            "Todo" => CaptureDestination.Todo,
            "Note" => CaptureDestination.Note,
            "Memory" => CaptureDestination.Memory,
            "Project" => CaptureDestination.Project,
            _ => null
        };
        var saved = _baseExperience.CommitCapture(draft, force);
        ShowHostStatus(saved is null ? "Capture refused (empty or secret-like)." : "Saved as " + saved.Kind);
    }

    private async Task ShowMemoryDialogAsync()
    {
        if (_baseExperience is null)
        {
            return;
        }

        AllowFullWindowInput();
        var search = new TextBox { Header = "Search", PlaceholderText = "Filter memories" };
        var list = new ListBox { Height = 240 };
        void Refresh()
        {
            var items = _baseExperience.Memory.RecallRanked(_baseExperience.Now, search.Text, take: 40);
            list.ItemsSource = items
                .Select(item => $"{item.Kind} · {item.Importance} · {item.Summary} [{item.Id}]")
                .ToList();
            list.Tag = items;
        }

        search.TextChanged += (_, _) => Refresh();
        Refresh();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(search);
        panel.Children.Add(list);
        var dialog = new ContentDialog
        {
            Title = "What does Secret Base remember?",
            Content = panel,
            PrimaryButtonText = "Forget selected",
            SecondaryButtonText = "Privacy",
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary
            && list.SelectedIndex >= 0
            && list.Tag is IReadOnlyList<MemoryEntry> tagged
            && list.SelectedIndex < tagged.Count)
        {
            _baseExperience.Memory.Forget(tagged[list.SelectedIndex].Id);
            ShowHostStatus("Forgotten.");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ShowPrivacyDialogAsync();
        }
    }

    private async Task ShowPrivacyDialogAsync()
    {
        if (_baseExperience is null || _baseSettings is null)
        {
            return;
        }

        AllowFullWindowInput();
        var quietStart = new NumberBox { Header = "Quiet hours start", Value = _baseSettings.QuietHoursStart, Minimum = 0, Maximum = 23 };
        var quietEnd = new NumberBox { Header = "Quiet hours end", Value = _baseSettings.QuietHoursEnd, Minimum = 0, Maximum = 23 };
        var focus = new NumberBox { Header = "Default focus minutes", Value = _baseSettings.DefaultFocusMinutes, Minimum = 5, Maximum = 90 };
        var allowFocus = new CheckBox { Content = "Allow interruptions during focus", IsChecked = _baseSettings.AllowFocusInterruptions };
        var body = new TextBlock
        {
            Text = _baseExperience.Privacy()
                + Environment.NewLine
                + Environment.NewLine
                + PersonalSpaceCatalog.Rules(_baseExperience.Rules),
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var scroll = new ScrollViewer { MaxHeight = 280, Content = body };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(scroll);
        panel.Children.Add(quietStart);
        panel.Children.Add(quietEnd);
        panel.Children.Add(focus);
        panel.Children.Add(allowFocus);
        var dialog = new ContentDialog
        {
            Title = "Privacy & automation",
            Content = panel,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Rules",
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _baseSettings.QuietHoursStart = (int)quietStart.Value;
            _baseSettings.QuietHoursEnd = (int)quietEnd.Value;
            _baseSettings.DefaultFocusMinutes = (int)focus.Value;
            _baseSettings.AllowFocusInterruptions = allowFocus.IsChecked == true;
            _baseSettingsStore?.Save(_baseSettings);
            _baseExperience.Preferences = _baseSettings;
            ShowHostStatus("Quiet hours saved.");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ShowAutomationRulesDialogAsync();
        }
    }

    private async Task ShowIntegrationsDialogAsync()
    {
        if (_baseExperience is null)
        {
            return;
        }

        AllowFullWindowInput();
        var items = _integrationHost?.Registry.List() ?? [];
        var list = new ListBox
        {
            Height = 220,
            ItemsSource = items.Select(IntegrationHost.FormatRegistration).ToList()
        };
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = _baseExperience.IntegrationPermissions(),
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(list);
        var dialog = new ContentDialog
        {
            Title = "My Integrations",
            Content = new ScrollViewer { MaxHeight = 360, Content = panel },
            PrimaryButtonText = "Enable example apps",
            SecondaryButtonText = "Permissions",
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && _integrationHost is not null)
        {
            DemoManifests.TryRegisterKnown(_integrationHost.Registry, approveDemos: true);
            ShowHostStatus("Example integrations are available.");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            await ShowIntegrationPermissionsDialogAsync();
        }
    }

    private async Task ShowIntegrationPermissionsDialogAsync()
    {
        if (_integrationHost is null)
        {
            return;
        }

        AllowFullWindowInput();
        var items = _integrationHost.Registry.List().Where(item => item.Approved).ToList();
        if (items.Count == 0)
        {
            await ShowTextDialogAsync("Permissions", _baseExperience?.IntegrationPermissions() ?? "No integrations.");
            return;
        }

        var selected = items[0];
        var rows = IntegrationPermissionGate.Matrix(selected.Manifest, selected.Permissions);
        var checks = rows.Select(row => new CheckBox
        {
            Content = row.Label,
            IsChecked = row.Granted,
            Tag = row.Flag
        }).ToList();
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = selected.DisplayName });
        foreach (var check in checks)
        {
            panel.Children.Add(check);
        }

        var dialog = new ContentDialog
        {
            Title = "Integration permissions",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var granted = IntegrationPermissionKind.None;
        foreach (var check in checks)
        {
            if (check.IsChecked == true && check.Tag is IntegrationPermissionKind flag)
            {
                granted |= flag;
            }
        }

        _integrationHost.Registry.SetPermissions(selected.Id, granted);
        ShowHostStatus("Permissions saved.");
    }

    private async Task ShowAutomationRulesDialogAsync()
    {
        if (_baseExperience is null)
        {
            return;
        }

        AllowFullWindowInput();
        var rules = _baseExperience.Rules.List().ToList();
        var list = new ListBox
        {
            Height = 240,
            ItemsSource = rules.Select(rule => $"{(rule.Enabled ? "On" : "Off")}  {rule.Name}").ToList()
        };
        var dialog = new ContentDialog
        {
            Title = "Automation permission",
            Content = list,
            PrimaryButtonText = "Disable selected",
            SecondaryButtonText = "Enable selected",
            CloseButtonText = "Close",
            XamlRoot = XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (list.SelectedIndex < 0 || list.SelectedIndex >= rules.Count)
        {
            return;
        }

        var selected = rules[list.SelectedIndex];
        if (result == ContentDialogResult.Primary)
        {
            selected.Enabled = false;
            _baseExperience.Rules.Save(selected);
            ShowHostStatus("Rule disabled.");
        }
        else if (result == ContentDialogResult.Secondary)
        {
            selected.Enabled = true;
            _baseExperience.Rules.Save(selected);
            ShowHostStatus("Rule enabled.");
        }
    }

    private async Task ShowTextDialogAsync(string title, string body, string? primary = null, Func<Task>? onPrimary = null)
    {
        AllowFullWindowInput();
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock { Text = body, TextWrapping = TextWrapping.WrapWholeWords }
            },
            PrimaryButtonText = primary ?? "Close",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };
        if (primary is not null)
        {
            dialog.CloseButtonText = "Cancel";
        }
        var result = await dialog.ShowAsync();
        if (primary is not null && result == ContentDialogResult.Primary && onPrimary is not null)
        {
            await onPrimary();
        }
    }
}
