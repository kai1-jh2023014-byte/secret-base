using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Assistant;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Assistant;

/// <summary>
/// Secret Base AI workspace chat. Tools go through IAssistantService → existing Commands.
/// No Host Bridge. API keys never rendered in the transcript.
/// </summary>
public sealed partial class AssistantWidgetView : UserControl
{
    private IAssistantService? _assistant;
    private IAssistantSettingsStore? _settingsStore;
    private ISecureSecretStore? _secrets;
    private IAiProviderFactory? _providers;
    private Func<AssistantTurnResult, string?>? _applyLaunch;
    private ThemeDefinition? _theme;
    private bool _busy;
    private readonly List<(string Kind, string Text)> _sessionLines = [];

    public AssistantWidgetView()
    {
        InitializeComponent();
    }

    public void Initialize(
        IAssistantService assistant,
        IAssistantSettingsStore settingsStore,
        ISecureSecretStore secrets,
        IAiProviderFactory providers,
        Func<AssistantTurnResult, string?> applyLaunch)
    {
        _assistant = assistant;
        _settingsStore = settingsStore;
        _secrets = secrets;
        _providers = providers;
        _applyLaunch = applyLaunch;
        StatusLabel.Text = string.Empty;
        RefreshProviderStatus();
        SeedFromHistory();
        RenderTranscript();
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
        SubtitleText.FontFamily = font;
        SubtitleText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        ProviderStatusText.FontFamily = font;
        ProviderStatusText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        OnboardingText.FontFamily = font;
        OnboardingText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        StatusLabel.FontFamily = font;
        StatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        ConfirmText.FontFamily = font;
        ConfirmText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        InputBox.FontFamily = font;
        StyleActionButton(SendButton, theme, accent: true);
        StyleActionButton(SettingsButton, theme);
        StyleActionButton(OpenSettingsFromOnboardingButton, theme, accent: true);
        StyleActionButton(ConfirmRunButton, theme, accent: true);
        StyleActionButton(ConfirmCancelButton, theme);
        RefreshProviderStatus();
        RenderTranscript();
    }

    private void RefreshProviderStatus()
    {
        var status = _assistant?.ProviderStatus;
        if (status is null && _settingsStore is not null)
        {
            var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
            var hasKey = _secrets is not null
                         && _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var key)
                         && !string.IsNullOrWhiteSpace(key);
            ProviderStatusText.Text = hasKey
                ? $"● Connected · {settings.ProviderId} · {settings.Model} · max {settings.MaxSteps} steps · key { (hasKey ? "••••••••" : "(not set)") }"
                : $"○ Not configured · {settings.ProviderId} · {settings.Model}";
            OnboardingPanel.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        if (status is null)
        {
            ProviderStatusText.Text = string.Empty;
            OnboardingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var mark = status.IsConfigured ? "● Connected" : "○ " + status.StatusLabel;
        ProviderStatusText.Text =
            $"{mark} · {status.ProviderId} · {status.Model} · max {status.MaxSteps} steps · key {status.ApiKeyDisplay}";
        OnboardingPanel.Visibility = status.IsConfigured ? Visibility.Collapsed : Visibility.Visible;
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

    private void SeedFromHistory()
    {
        if (_sessionLines.Count > 0 || _assistant is null)
        {
            return;
        }

        foreach (var message in _assistant.VisibleHistory)
        {
            _sessionLines.Add((
                message.Role == AiMessageRole.User ? "You" : "AI",
                message.Content ?? string.Empty));
        }
    }

    private void RenderTranscript()
    {
        Transcript.Children.Clear();
        foreach (var line in _sessionLines)
        {
            Transcript.Children.Add(line.Kind is "You" or "AI"
                ? CreateBubble(line.Kind, line.Text)
                : CreateActivity(line.Text, error: line.Kind == "Error"));
        }

        ScrollToEnd();
    }

    private UIElement CreateBubble(string who, string text)
    {
        var title = new TextBlock
        {
            Text = who,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Opacity = 0.8
        };
        var body = new TextBlock
        {
            Text = text,
            FontSize = 13,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        if (_theme is not null)
        {
            title.FontFamily = new FontFamily(_theme.FontFamily);
            title.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
            body.FontFamily = new FontFamily(_theme.FontFamily);
            body.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
        }

        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(title);
        panel.Children.Add(body);
        return panel;
    }

    private UIElement CreateActivity(string text, bool error)
    {
        var line = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Opacity = error ? 1.0 : 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        if (_theme is not null)
        {
            line.FontFamily = new FontFamily(_theme.FontFamily);
            line.Foreground = ThemePainter.Brush(error ? _theme.Accent : _theme.ForegroundMuted);
        }

        return line;
    }

    private void ScrollToEnd()
    {
        TranscriptScroll.UpdateLayout();
        TranscriptScroll.ChangeView(null, TranscriptScroll.ScrollableHeight, null);
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = SendAsync();
        }
    }

    private void SendButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync();

    private async Task SendAsync()
    {
        if (_busy || _assistant is null)
        {
            return;
        }

        var text = InputBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputBox.Text = string.Empty;
        _sessionLines.Add(("You", text));
        RenderTranscript();
        await RunTurnAsync(() => _assistant.SendAsync(text)).ConfigureAwait(true);
    }

    private async void ConfirmRun_Click(object sender, RoutedEventArgs e)
    {
        if (_assistant is null)
        {
            return;
        }

        await RunTurnAsync(() => _assistant.ConfirmPendingAsync()).ConfigureAwait(true);
    }

    private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_assistant is null)
        {
            return;
        }

        _assistant.CancelPending();
        HideConfirm();
        _sessionLines.Add(("Activity", AssistantUserMessages.ActionCancelled));
        RenderTranscript();
        StatusLabel.Text = AssistantUserMessages.ActionCancelled;
        _ = RunTurnAsync(() => _assistant.ContinueAfterCancelAsync());
    }

    private async Task RunTurnAsync(Func<Task<AssistantTurnResult>> turn)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        SendButton.IsEnabled = false;
        InputBox.IsEnabled = false;
        StatusLabel.Text = "Working…";
        HideConfirm();
        RefreshProviderStatus();
        try
        {
            var result = await turn().ConfigureAwait(true);

            if (result.Plan is not null)
            {
                _sessionLines.Add(("Activity", result.Plan.FormatForUi()));
            }

            foreach (var activity in result.Activities)
            {
                if (string.IsNullOrWhiteSpace(activity.Text))
                {
                    continue;
                }

                if (string.Equals(activity.Text, "Plan prepared", StringComparison.Ordinal))
                {
                    continue;
                }

                var label = activity.Status switch
                {
                    AssistantActivityStatus.Done when activity.Domain is not null
                        && !activity.Text.Contains('✓', StringComparison.Ordinal) =>
                        $"{activity.Domain} ✓",
                    AssistantActivityStatus.Failed when activity.Domain is not null =>
                        $"{activity.Domain} ✗ — {activity.Text}",
                    AssistantActivityStatus.PendingConfirmation => activity.Text,
                    _ => activity.Text
                };
                _sessionLines.Add(("Activity", label));
            }

            foreach (var actionResult in result.ActionResults)
            {
                var mark = actionResult.Succeeded ? "✓" : "✗";
                _sessionLines.Add(("Activity", $"{mark} {actionResult.Label}: {actionResult.Message}"));
            }

            if (result.PendingConfirmation is not null)
            {
                ShowConfirm(result.PendingConfirmation);
                StatusLabel.Text = "Confirmation required.";
                RenderTranscript();
                return;
            }

            if (!result.Succeeded)
            {
                var error = result.ErrorMessage ?? AssistantUserMessages.Unavailable;
                _sessionLines.Add(("Error", error));
                StatusLabel.Text = error;
                if (result.NeedsConfiguration)
                {
                    OnboardingPanel.Visibility = Visibility.Visible;
                }

                RenderTranscript();
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.AssistantText))
            {
                _sessionLines.Add(("AI", result.AssistantText!));
            }

            var launchError = _applyLaunch?.Invoke(result);
            if (!string.IsNullOrWhiteSpace(launchError))
            {
                var shown = result.ShouldOpenCursorAtFolder
                    ? AssistantUserMessages.CursorOpenFailed
                    : launchError;
                _sessionLines.Add(("Error", shown));
                StatusLabel.Text = shown;
            }
            else if (result.ShouldOpenCursorAtFolder)
            {
                _sessionLines.Add(("Activity", "✓ " + AssistantUserMessages.CursorOpenSucceeded));
                StatusLabel.Text = AssistantUserMessages.CursorOpenSucceeded;
            }
            else
            {
                StatusLabel.Text = result.ResponseKind switch
                {
                    AssistantResponseKind.Suggest => "Suggestion — say open/launch to run.",
                    AssistantResponseKind.Plan => "Plan ready.",
                    AssistantResponseKind.Execute => "Done.",
                    _ => string.Empty
                };
            }

            RenderTranscript();
        }
        catch (Exception)
        {
            StatusLabel.Text = AssistantUserMessages.Unavailable;
            _sessionLines.Add(("Error", AssistantUserMessages.Unavailable));
            RenderTranscript();
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
            InputBox.IsEnabled = true;
            RefreshProviderStatus();
        }
    }

    private void ShowConfirm(AssistantPendingConfirmation pending)
    {
        ConfirmText.Text = pending.Prompt;
        ConfirmActionList.Children.Clear();
        if (pending.Actions.Count > 1)
        {
            foreach (var action in pending.Actions)
            {
                var line = new TextBlock
                {
                    Text = "✓ " + action.Label,
                    FontSize = 12,
                    TextWrapping = TextWrapping.WrapWholeWords
                };
                if (_theme is not null)
                {
                    line.FontFamily = new FontFamily(_theme.FontFamily);
                    line.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
                }

                ConfirmActionList.Children.Add(line);
            }
        }

        if (pending.HasRiskyAction)
        {
            var risk = new TextBlock
            {
                Text = "Includes Host launch (e.g. Cursor).",
                FontSize = 11,
                Opacity = 0.85,
                TextWrapping = TextWrapping.WrapWholeWords
            };
            if (_theme is not null)
            {
                risk.FontFamily = new FontFamily(_theme.FontFamily);
                risk.Foreground = ThemePainter.Brush(_theme.Accent);
            }

            ConfirmActionList.Children.Add(risk);
        }

        ConfirmPanel.Visibility = Visibility.Visible;
    }

    private void HideConfirm()
    {
        ConfirmPanel.Visibility = Visibility.Collapsed;
        ConfirmText.Text = string.Empty;
        ConfirmActionList.Children.Clear();
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        await ShowSettingsAsync();

    private async Task ShowSettingsAsync()
    {
        if (_settingsStore is null || _secrets is null || _providers is null)
        {
            return;
        }

        var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
        var providerBox = new ComboBox
        {
            Header = "AI Provider",
            ItemsSource = new[] { "OpenAI", "Gemini", "Local" },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        providerBox.SelectedItem = settings.ProviderId switch
        {
            AssistantProviderIds.Gemini => "Gemini",
            AssistantProviderIds.Local => "Local",
            _ => "OpenAI"
        };

        var modelBox = new TextBox
        {
            Header = "Model",
            Text = settings.Model
        };

        var maxStepsBox = new NumberBox
        {
            Header = "Max steps (plan / tools)",
            Value = settings.MaxSteps,
            Minimum = AssistantSettings.MinMaxSteps,
            Maximum = AssistantSettings.MaxStepsHardCap,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var confirmBox = new ToggleSwitch
        {
            Header = "Require confirmation for actions",
            IsOn = settings.RequireConfirmationForActions
        };

        var hasKey = _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var existing)
                     && !string.IsNullOrWhiteSpace(existing);
        var keyStatus = new TextBlock
        {
            Text = hasKey ? "API Key  ••••••••  (saved in Credential Manager)" : "API Key  (not set)",
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var keyBox = new PasswordBox
        {
            Header = hasKey ? "Change API Key (leave blank to keep)" : "API Key",
            PlaceholderText = hasKey ? "••••••••" : "sk-…"
        };

        var testStatus = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var testButton = new Button { Content = "Test Connection", MinWidth = 120 };
        testButton.Click += async (_, _) =>
        {
            PersistSettingsFromDialog(providerBox, modelBox, maxStepsBox, confirmBox, keyBox, keepExistingIfBlank: true);
            var current = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
            var provider = _providers.Create(current);
            var ping = await provider.ChatAsync(
                [new AiMessage { Role = AiMessageRole.User, Content = "Reply with the single word pong." }],
                Array.Empty<AssistantToolDefinition>(),
                current.Model);
            testStatus.Text = ping.Status switch
            {
                AiProviderStatus.Ok => "● Connected",
                AiProviderStatus.NotConfigured =>
                    AssistantUserMessages.NotConfigured + " " + AssistantUserMessages.OpenSettings,
                _ => ping.ErrorMessage ?? AssistantUserMessages.Unavailable
            };
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(providerBox);
        panel.Children.Add(modelBox);
        panel.Children.Add(maxStepsBox);
        panel.Children.Add(confirmBox);
        panel.Children.Add(keyStatus);
        panel.Children.Add(keyBox);
        panel.Children.Add(testButton);
        panel.Children.Add(testStatus);

        var dialog = new ContentDialog
        {
            Title = "AI Settings",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        PersistSettingsFromDialog(providerBox, modelBox, maxStepsBox, confirmBox, keyBox, keepExistingIfBlank: true);
        StatusLabel.Text = "AI settings saved.";
        RefreshProviderStatus();
    }

    private void PersistSettingsFromDialog(
        ComboBox providerBox,
        TextBox modelBox,
        NumberBox maxStepsBox,
        ToggleSwitch confirmBox,
        PasswordBox keyBox,
        bool keepExistingIfBlank)
    {
        if (_settingsStore is null || _secrets is null)
        {
            return;
        }

        var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
        settings.ProviderId = (providerBox.SelectedItem as string) switch
        {
            "Gemini" => AssistantProviderIds.Gemini,
            "Local" => AssistantProviderIds.Local,
            _ => AssistantProviderIds.OpenAi
        };
        settings.Model = string.IsNullOrWhiteSpace(modelBox.Text)
            ? AssistantSettings.DefaultOpenAiModel
            : modelBox.Text.Trim();
        settings.MaxSteps = (int)Math.Clamp(
            double.IsNaN(maxStepsBox.Value) ? AssistantSettings.DefaultMaxSteps : maxStepsBox.Value,
            AssistantSettings.MinMaxSteps,
            AssistantSettings.MaxStepsHardCap);
        settings.RequireConfirmationForActions = confirmBox.IsOn;
        _settingsStore.Save(settings);

        var typed = keyBox.Password?.Trim();
        if (!string.IsNullOrWhiteSpace(typed))
        {
            _secrets.SetSecret(AssistantSecretKeys.OpenAiApiKey, typed);
        }
        else if (!keepExistingIfBlank)
        {
            _secrets.DeleteSecret(AssistantSecretKeys.OpenAiApiKey);
        }
    }
}
