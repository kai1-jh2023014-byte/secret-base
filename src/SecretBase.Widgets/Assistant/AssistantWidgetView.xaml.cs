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
        StatusLabel.FontFamily = font;
        StatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        ConfirmText.FontFamily = font;
        ConfirmText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        InputBox.FontFamily = font;
        StyleActionButton(SendButton, theme, accent: true);
        StyleActionButton(SettingsButton, theme);
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
            var settings = _settingsStore.LoadOrCreate();
            var hasKey = _secrets is not null
                         && _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var key)
                         && !string.IsNullOrWhiteSpace(key);
            ProviderStatusText.Text = hasKey
                ? $"Provider: {settings.ProviderId} · {settings.Model} · Configured"
                : $"Provider: {settings.ProviderId} · {settings.Model} · Not configured";
            return;
        }

        if (status is null)
        {
            ProviderStatusText.Text = string.Empty;
            return;
        }

        ProviderStatusText.Text =
            $"Provider: {status.ProviderId} · {status.Model} · {status.StatusLabel}";
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
        _sessionLines.Add(("Activity", "Cancelled."));
        RenderTranscript();
        StatusLabel.Text = "Cancelled.";
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
        StatusLabel.Text = "Thinking…";
        HideConfirm();
        RefreshProviderStatus();
        try
        {
            var result = await turn().ConfigureAwait(true);
            foreach (var activity in result.Activities)
            {
                if (string.IsNullOrWhiteSpace(activity.Text))
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

            if (result.PendingConfirmation is not null)
            {
                ShowConfirm(result.PendingConfirmation.Prompt);
                StatusLabel.Text = "Confirmation required.";
                RenderTranscript();
                return;
            }

            if (!result.Succeeded)
            {
                var error = result.ErrorMessage ?? AssistantUserMessages.Unavailable;
                _sessionLines.Add(("Error", error));
                StatusLabel.Text = error;
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
            else
            {
                StatusLabel.Text = result.ResponseKind == AssistantResponseKind.Suggest
                    ? "Suggestion — say open/launch to run."
                    : string.Empty;
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

    private void ShowConfirm(string prompt)
    {
        ConfirmText.Text = prompt;
        ConfirmPanel.Visibility = Visibility.Visible;
    }

    private void HideConfirm()
    {
        ConfirmPanel.Visibility = Visibility.Collapsed;
        ConfirmText.Text = string.Empty;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        await ShowSettingsAsync();

    private async Task ShowSettingsAsync()
    {
        if (_settingsStore is null || _secrets is null || _providers is null)
        {
            return;
        }

        var settings = _settingsStore.LoadOrCreate();
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

        var hasKey = _secrets.TryGetSecret(AssistantSecretKeys.OpenAiApiKey, out var existing)
                     && !string.IsNullOrWhiteSpace(existing);
        var keyBox = new PasswordBox
        {
            Header = hasKey ? "API Key (saved — leave blank to keep)" : "API Key",
            PlaceholderText = hasKey ? "********" : "sk-…"
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
            PersistSettingsFromDialog(providerBox, modelBox, keyBox, keepExistingIfBlank: true);
            var current = _settingsStore.LoadOrCreate();
            var provider = _providers.Create(current);
            var ping = await provider.ChatAsync(
                [new AiMessage { Role = AiMessageRole.User, Content = "Reply with the single word pong." }],
                Array.Empty<AssistantToolDefinition>(),
                current.Model);
            testStatus.Text = ping.Status switch
            {
                AiProviderStatus.Ok => "Connection OK.",
                AiProviderStatus.NotConfigured =>
                    AssistantUserMessages.NotConfigured + " " + AssistantUserMessages.OpenSettings,
                _ => ping.ErrorMessage ?? AssistantUserMessages.Unavailable
            };
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(providerBox);
        panel.Children.Add(modelBox);
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

        PersistSettingsFromDialog(providerBox, modelBox, keyBox, keepExistingIfBlank: true);
        StatusLabel.Text = "AI settings saved.";
        RefreshProviderStatus();
    }

    private void PersistSettingsFromDialog(
        ComboBox providerBox,
        TextBox modelBox,
        PasswordBox keyBox,
        bool keepExistingIfBlank)
    {
        if (_settingsStore is null || _secrets is null)
        {
            return;
        }

        var settings = _settingsStore.LoadOrCreate();
        settings.ProviderId = (providerBox.SelectedItem as string) switch
        {
            "Gemini" => AssistantProviderIds.Gemini,
            "Local" => AssistantProviderIds.Local,
            _ => AssistantProviderIds.OpenAi
        };
        settings.Model = string.IsNullOrWhiteSpace(modelBox.Text)
            ? AssistantSettings.DefaultOpenAiModel
            : modelBox.Text.Trim();
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
