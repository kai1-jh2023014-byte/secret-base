using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Assistant;
using SecretBase.Core.Jev;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Hosting;
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
    private IJevDecisionService? _jev;
    private Func<AssistantTurnResult, string?>? _applyLaunch;
    private OverlayDialogInput? _dialogInput;
    private ThemeDefinition? _theme;
    private bool _busy;
    private string? _lastRetryUserText;
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
        Func<AssistantTurnResult, string?> applyLaunch,
        string? headerTitle = null,
        string? headerSubtitle = null,
        OverlayDialogInput? dialogInput = null,
        IJevDecisionService? jev = null)
    {
        _assistant = assistant;
        _settingsStore = settingsStore;
        _secrets = secrets;
        _providers = providers;
        _applyLaunch = applyLaunch;
        _jev = jev;
        _dialogInput = dialogInput;
        if (!string.IsNullOrWhiteSpace(headerTitle))
        {
            HeaderText.Text = headerTitle;
        }

        if (!string.IsNullOrWhiteSpace(headerSubtitle))
        {
            SubtitleText.Text = headerSubtitle;
        }

        StatusLabel.Text = string.Empty;
        RefreshProviderStatus();
        SeedFromHistory();
        RenderTranscript();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SubtitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(ProviderStatusText, theme);
        WidgetSurfaceStyle.ApplyBody(OnboardingText, theme);
        WidgetSurfaceStyle.ApplyMuted(CapabilitiesText, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyBody(ConfirmText, theme);
        InputBox.FontFamily = new FontFamily(theme.FontFamily);
        WidgetSurfaceStyle.ApplyActionButton(SendButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(SettingsButton, theme);
        WidgetSurfaceStyle.ApplyActionButton(OpenSettingsFromOnboardingButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyActionButton(ConfirmRunButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ConfirmCancelButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(RetryButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(OpenSettingsFromErrorButton, theme);
        RefreshProviderStatus();
        RenderTranscript();
    }

    private void RefreshProviderStatus()
    {
        var status = _assistant?.ProviderStatus;
        if (status is null && _settingsStore is not null)
        {
            var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
            var hasKey = HasKeyForProvider(settings.ProviderId);
            ProviderStatusText.Text = BaseAiStatusFormatter.Format(new AssistantProviderStatusInfo
            {
                ProviderId = settings.ProviderId,
                IsConfigured = hasKey || string.Equals(
                    settings.ProviderId,
                    AssistantProviderIds.Local,
                    StringComparison.OrdinalIgnoreCase)
            });
            OnboardingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (status is null)
        {
            ProviderStatusText.Text = string.Empty;
            OnboardingPanel.Visibility = Visibility.Collapsed;
            return;
        }

        ProviderStatusText.Text = BaseAiStatusFormatter.Format(status)
                                  + (string.IsNullOrWhiteSpace(status.FallbackNote) ? string.Empty : " · " + status.FallbackNote);
        OnboardingPanel.Visibility = Visibility.Collapsed;
    }

    private static void StyleActionButton(Button button, ThemeDefinition theme, bool accent = false) =>
        WidgetSurfaceStyle.ApplyActionButton(button, theme, accent);

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

        if (_assistant.HasPendingConfirmation)
        {
            StatusLabel.Text = AssistantUserMessages.PendingConfirmationMustResolve;
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
        if (_assistant is null || _busy)
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
        StatusLabel.Text = "Thinking…";
        HideConfirm();
        HideErrorActions();
        RefreshProviderStatus();
        try
        {
            var result = await turn().ConfigureAwait(true);
            _lastRetryUserText = result.RetryUserText;

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
                ShowErrorActions(result);
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
                ShowErrorActions(new AssistantTurnResult
                {
                    Succeeded = false,
                    ErrorMessage = shown,
                    CanRetry = true,
                    RetryUserText = _lastRetryUserText
                });
            }
            else if (result.ShouldOpenCursorAtFolder)
            {
                _sessionLines.Add(("Activity", "✓ " + AssistantUserMessages.CursorOpenSucceeded));
                StatusLabel.Text = AssistantUserMessages.CursorOpenSucceeded;
                HideErrorActions();
            }
            else
            {
                HideErrorActions();
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
            ShowErrorActions(new AssistantTurnResult
            {
                Succeeded = false,
                ErrorMessage = AssistantUserMessages.Unavailable,
                CanRetry = true,
                RetryUserText = _lastRetryUserText
            });
            RenderTranscript();
        }
        finally
        {
            _busy = false;
            var awaitingConfirm = _assistant?.HasPendingConfirmation ?? false;
            SendButton.IsEnabled = !awaitingConfirm;
            InputBox.IsEnabled = !awaitingConfirm;
            ConfirmRunButton.IsEnabled = awaitingConfirm;
            ConfirmCancelButton.IsEnabled = awaitingConfirm;
            RefreshProviderStatus();
        }
    }

    private void ShowConfirm(AssistantPendingConfirmation pending)
    {
        ConfirmText.Text = pending.Prompt;
        ConfirmActionList.Children.Clear();
        if (pending.Actions.Count > 1)
        {
            ConfirmRunButton.Content = "Run all";
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
        else
        {
            ConfirmRunButton.Content = "Run";
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
        ConfirmRunButton.Content = "Run";
    }

    private void ShowErrorActions(AssistantTurnResult result)
    {
        RetryButton.Visibility = result.CanRetry && !string.IsNullOrWhiteSpace(result.RetryUserText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        OpenSettingsFromErrorButton.Visibility = result.ShowOpenSettingsAction || result.NeedsConfiguration
            ? Visibility.Visible
            : Visibility.Collapsed;
        ErrorActionPanel.Visibility =
            RetryButton.Visibility == Visibility.Visible || OpenSettingsFromErrorButton.Visibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void HideErrorActions()
    {
        ErrorActionPanel.Visibility = Visibility.Collapsed;
        RetryButton.Visibility = Visibility.Collapsed;
        OpenSettingsFromErrorButton.Visibility = Visibility.Collapsed;
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_assistant is null || string.IsNullOrWhiteSpace(_lastRetryUserText)
            || _assistant.HasPendingConfirmation)
        {
            return;
        }

        _sessionLines.Add(("You", _lastRetryUserText!));
        RenderTranscript();
        await RunTurnAsync(() => _assistant.SendAsync(_lastRetryUserText!)).ConfigureAwait(true);
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        await ShowSettingsAsync();

    private async Task ShowSettingsAsync()
    {
        if (_settingsStore is null || _secrets is null || _providers is null)
        {
            return;
        }

        using var _ = _dialogInput?.Enter();

        var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
        var conversationHeader = new TextBlock
        {
            Text = "Conversation AI",
            FontSize = 14
        };
        var providerBox = new ComboBox
        {
            Header = "Provider",
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

        var providerHelp = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var keyStatus = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var keyBox = new PasswordBox();

        void RefreshProviderUi(bool resetModelIfNeeded)
        {
            var selected = providerBox.SelectedItem as string;
            providerHelp.Text = selected switch
            {
                "Gemini" => "Gemini uses your Google AI Studio key (saved separately from OpenAI). Falls back to Local AI when unavailable.",
                "Local" => "Local AI uses Ollama at the configured endpoint. No API key required.",
                _ => "OpenAI uses your API key (Credential Manager). Falls back to Local AI when unavailable."
            };

            if (resetModelIfNeeded)
            {
                modelBox.Text = selected switch
                {
                    "Gemini" => AssistantSettings.DefaultGeminiModel,
                    "Local" => AssistantSettings.DefaultLocalModel,
                    _ => AssistantSettings.DefaultOpenAiModel
                };
            }

            if (selected == "Local")
            {
                keyStatus.Text = "API Key  (not required for Local)";
                keyBox.Header = "API Key (unused for Local)";
                keyBox.PlaceholderText = "—";
                keyBox.IsEnabled = false;
                return;
            }

            keyBox.IsEnabled = true;
            var secretKey = selected == "Gemini"
                ? AssistantSecretKeys.GeminiApiKey
                : AssistantSecretKeys.OpenAiApiKey;
            var hasKey = _secrets.TryGetSecret(secretKey, out var existing)
                         && !string.IsNullOrWhiteSpace(existing);
            keyStatus.Text = hasKey
                ? $"{selected} API Key  ••••••••  (saved in Credential Manager)"
                : $"{selected} API Key  (not set)";
            keyBox.Header = hasKey ? $"Change {selected} API Key (leave blank to keep)" : $"{selected} API Key";
            keyBox.PlaceholderText = hasKey ? "••••••••" : selected == "Gemini" ? "AIza…" : "sk-…";
        }

        providerBox.SelectionChanged += (_, _) => RefreshProviderUi(resetModelIfNeeded: true);
        RefreshProviderUi(resetModelIfNeeded: false);

        var maxStepsBox = new NumberBox
        {
            Header = "Max steps (plan / tools)",
            Value = settings.MaxSteps,
            Minimum = AssistantSettings.MinMaxSteps,
            Maximum = AssistantSettings.MaxStepsHardCap,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
        };

        var confirmNote = new TextBlock
        {
            Text = "Launch actions always require Run confirmation. Keys are never shown in chat.",
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var jevSaved = _secrets.TryGetSecret(JevSecretKeys.ApiKey, out var jevExisting)
                       && !string.IsNullOrWhiteSpace(jevExisting);
        var jevKeyBox = new PasswordBox
        {
            Header = jevSaved ? "Change Jev API Key (leave blank to keep)" : "Jev API Key",
            PlaceholderText = jevSaved ? "••••••••" : "Jev key"
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
            var saveWarning = PersistSettingsFromDialog(providerBox, modelBox, maxStepsBox, keyBox, jevKeyBox, keepExistingIfBlank: true);
            var current = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
            var provider = _providers.Create(current);
            var ping = await provider.ChatAsync(
                [new AiMessage { Role = AiMessageRole.User, Content = "Reply with the single word pong." }],
                Array.Empty<AssistantToolDefinition>(),
                current.Model);
            var pingText = ping.Status switch
            {
                AiProviderStatus.Ok when string.Equals(provider.ProviderId, AssistantProviderIds.Local, StringComparison.OrdinalIgnoreCase)
                    => "● Local AI answered. The selected remote provider was not used.",
                AiProviderStatus.Ok => $"● Connected — {provider.DisplayName} answered.",
                AiProviderStatus.NotConfigured =>
                    AssistantUserMessages.NotConfigured + " Save the key for the selected provider, then test again.",
                _ => ping.ErrorMessage ?? AssistantUserMessages.Unavailable
            };
            testStatus.Text = saveWarning is null ? pingText : saveWarning + " " + pingText;
        };

        var jevHeader = new TextBlock
        {
            Text = "Jev Decision AI",
            FontSize = 14,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var jevHelp = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = "Jev decides the situation, the next step, and whether to confirm. It is not a chat provider and it cannot run the PC. A connection check sends only the words \"Secret Base connection check.\" A real decision sends the time, a local intent label, counts, and the current message with secrets removed."
        };
        var jevStatus = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords,
            Text = jevSaved
                ? "Jev API Key  ••••••••  (saved separately from conversation keys)"
                : "Jev API Key  (not set)"
        };
        var jevTestStatus = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        var jevTestButton = new Button { Content = "Test Jev", MinWidth = 120 };
        jevTestButton.Click += async (_, _) =>
        {
            var jevWarning = TrySaveJevKey(jevKeyBox, keepExistingIfBlank: true);
            if (jevWarning is not null)
            {
                jevTestStatus.Text = jevWarning;
                return;
            }

            if (_jev is null)
            {
                jevTestStatus.Text = "Jev decision AI is not available.";
                return;
            }

            var test = await _jev.TestConnectionAsync();
            jevTestStatus.Text = test.Message;
            var nowSaved = _secrets.TryGetSecret(JevSecretKeys.ApiKey, out var stored)
                           && !string.IsNullOrWhiteSpace(stored);
            jevStatus.Text = nowSaved
                ? "Jev API Key  ••••••••  (saved separately from conversation keys)"
                : "Jev API Key  (not set)";
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(conversationHeader);
        panel.Children.Add(providerBox);
        panel.Children.Add(providerHelp);
        panel.Children.Add(modelBox);
        panel.Children.Add(maxStepsBox);
        panel.Children.Add(confirmNote);
        panel.Children.Add(keyStatus);
        panel.Children.Add(keyBox);
        panel.Children.Add(testButton);
        panel.Children.Add(testStatus);
        panel.Children.Add(jevHeader);
        panel.Children.Add(jevHelp);
        panel.Children.Add(jevStatus);
        panel.Children.Add(jevKeyBox);
        panel.Children.Add(jevTestButton);
        panel.Children.Add(jevTestStatus);

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

        var warning = PersistSettingsFromDialog(providerBox, modelBox, maxStepsBox, keyBox, jevKeyBox, keepExistingIfBlank: true);
        StatusLabel.Text = warning ?? "AI settings saved.";
        RefreshProviderStatus();
    }

    private string? PersistSettingsFromDialog(
        ComboBox providerBox,
        TextBox modelBox,
        NumberBox maxStepsBox,
        PasswordBox keyBox,
        PasswordBox jevKeyBox,
        bool keepExistingIfBlank)
    {
        if (_settingsStore is null || _secrets is null)
        {
            return null;
        }

        var settings = AssistantSettingsMigrator.MigrateToCurrent(_settingsStore.LoadOrCreate());
        var typed = keyBox.Password?.Trim();
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(typed)
            && typed.StartsWith("jv_", StringComparison.Ordinal))
        {
            typed = null;
            warning = JevUserMessages.ConversationKeyIgnored;
        }

        var selected = providerBox.SelectedItem as string;
        if (!string.IsNullOrWhiteSpace(typed))
        {
            if (typed.StartsWith("AIza", StringComparison.Ordinal))
            {
                selected = "Gemini";
            }
            else if (typed.StartsWith("sk-", StringComparison.Ordinal))
            {
                selected = "OpenAI";
            }
        }

        settings.ProviderId = selected switch
        {
            "Gemini" => AssistantProviderIds.Gemini,
            "Local" => AssistantProviderIds.Local,
            _ => AssistantProviderIds.OpenAi
        };
        settings.Model = string.IsNullOrWhiteSpace(modelBox.Text)
            ? DefaultModelFor(settings.ProviderId)
            : modelBox.Text.Trim();
        if (string.Equals(settings.ProviderId, AssistantProviderIds.Gemini, StringComparison.OrdinalIgnoreCase)
            && (settings.Model.Contains("gpt-", StringComparison.OrdinalIgnoreCase)
                || settings.Model.Contains("llama", StringComparison.OrdinalIgnoreCase)))
        {
            settings.Model = AssistantSettings.DefaultGeminiModel;
        }

        settings.MaxSteps = (int)Math.Clamp(
            double.IsNaN(maxStepsBox.Value) ? AssistantSettings.DefaultMaxSteps : maxStepsBox.Value,
            AssistantSettings.MinMaxSteps,
            AssistantSettings.MaxStepsHardCap);
        settings.RequireConfirmationForActions = true;
        _settingsStore.Save(settings);

        if (!string.Equals(settings.ProviderId, AssistantProviderIds.Local, StringComparison.OrdinalIgnoreCase))
        {
            var secretKey = string.Equals(settings.ProviderId, AssistantProviderIds.Gemini, StringComparison.OrdinalIgnoreCase)
                ? AssistantSecretKeys.GeminiApiKey
                : AssistantSecretKeys.OpenAiApiKey;
            if (!string.IsNullOrWhiteSpace(typed))
            {
                _secrets.SetSecret(secretKey, typed);
            }
            else if (!keepExistingIfBlank)
            {
                _secrets.DeleteSecret(secretKey);
            }
        }

        var jevWarning = TrySaveJevKey(jevKeyBox, keepExistingIfBlank);
        return jevWarning ?? warning;
    }

    private string? TrySaveJevKey(PasswordBox jevKeyBox, bool keepExistingIfBlank)
    {
        if (_secrets is null)
        {
            return null;
        }

        var typed = jevKeyBox.Password?.Trim();
        if (string.IsNullOrWhiteSpace(typed))
        {
            if (!keepExistingIfBlank)
            {
                _secrets.DeleteSecret(JevSecretKeys.ApiKey);
            }

            return null;
        }

        if (typed.StartsWith("AIza", StringComparison.Ordinal)
            || typed.StartsWith("sk-", StringComparison.Ordinal))
        {
            return JevUserMessages.WrongKey;
        }

        _secrets.SetSecret(JevSecretKeys.ApiKey, typed);
        jevKeyBox.Password = string.Empty;
        return null;
    }

    private bool HasKeyForProvider(string? providerId)
    {
        if (_secrets is null)
        {
            return false;
        }

        if (string.Equals(providerId, AssistantProviderIds.Local, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var secretKey = string.Equals(providerId, AssistantProviderIds.Gemini, StringComparison.OrdinalIgnoreCase)
            ? AssistantSecretKeys.GeminiApiKey
            : AssistantSecretKeys.OpenAiApiKey;
        return _secrets.TryGetSecret(secretKey, out var key) && !string.IsNullOrWhiteSpace(key);
    }

    private static string DefaultModelFor(string providerId) =>
        providerId switch
        {
            AssistantProviderIds.Gemini => AssistantSettings.DefaultGeminiModel,
            AssistantProviderIds.Local => AssistantSettings.DefaultLocalModel,
            _ => AssistantSettings.DefaultOpenAiModel
        };
}
