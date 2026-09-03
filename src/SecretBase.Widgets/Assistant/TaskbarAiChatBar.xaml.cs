using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Assistant;
using SecretBase.Core.Themes;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Assistant;

/// <summary>
/// Dedicated Secret Base AI chat field for the overlay work area (above the Windows taskbar).
/// Does not hook Explorer, SearchHost, or Windows Search.
/// </summary>
public sealed partial class TaskbarAiChatBar : UserControl
{
    private IAssistantService? _assistant;
    private Func<AssistantTurnResult, string?>? _applyLaunch;
    private ThemeDefinition? _theme;
    private bool _busy;
    private readonly List<(string Kind, string Text)> _lines = [];

    public TaskbarAiChatBar()
    {
        InitializeComponent();
    }

    public event Action? LayoutChanged;

    public void Initialize(IAssistantService assistant, Func<AssistantTurnResult, string?> applyLaunch)
    {
        _assistant = assistant;
        _applyLaunch = applyLaunch;
        RefreshProvider();
        CollapseResults();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyChrome(ResultsShell, theme);
        WidgetSurfaceStyle.ApplyChrome(Pill, theme);
        Pill.CornerRadius = new CornerRadius(22);
        WidgetSurfaceStyle.ApplyHeader(BrandText, ProviderText, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyBody(ConfirmText, theme);
        InputBox.FontFamily = new FontFamily(theme.FontFamily);
        InputBox.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        WidgetSurfaceStyle.ApplyActionButton(SendButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyActionButton(ConfirmRunButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ConfirmCancelButton, theme);
        RefreshProvider();
        RenderTranscript();
    }

    public void FocusInput()
    {
        InputBox.Focus(FocusState.Programmatic);
        LayoutChanged?.Invoke();
    }

    private void RefreshProvider()
    {
        var status = _assistant?.ProviderStatus;
        if (status is null)
        {
            ProviderText.Text = BaseAiStatusFormatter.FormatShort(null);
            return;
        }

        ProviderText.Text = BaseAiStatusFormatter.FormatShort(status);
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            CollapseResults();
            return;
        }

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
            ExpandResultsIfNeeded();
            StatusLabel.Text = AssistantUserMessages.PendingConfirmationMustResolve;
            return;
        }

        var text = InputBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputBox.Text = string.Empty;
        _lines.Add(("You", text));
        ExpandResultsIfNeeded();
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
        ConfirmPanel.Visibility = Visibility.Collapsed;
        _lines.Add(("Activity", AssistantUserMessages.ActionCancelled));
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
        ConfirmPanel.Visibility = Visibility.Collapsed;
        RefreshProvider();
        try
        {
            var result = await turn().ConfigureAwait(true);
            if (result.PendingConfirmation is not null)
            {
                ConfirmText.Text = result.PendingConfirmation.Prompt;
                ConfirmPanel.Visibility = Visibility.Visible;
                StatusLabel.Text = "Confirmation required.";
                RenderTranscript();
                LayoutChanged?.Invoke();
                return;
            }

            if (!result.Succeeded)
            {
                var error = result.ErrorMessage ?? AssistantUserMessages.Unavailable;
                _lines.Add(("Error", error));
                StatusLabel.Text = error;
                RenderTranscript();
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.AssistantText))
            {
                _lines.Add(("AI", result.AssistantText!));
            }

            var launchError = _applyLaunch?.Invoke(result);
            if (!string.IsNullOrWhiteSpace(launchError))
            {
                _lines.Add(("Error", launchError));
                StatusLabel.Text = launchError;
            }
            else
            {
                StatusLabel.Text = string.Empty;
            }

            RenderTranscript();
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
            InputBox.IsEnabled = true;
            LayoutChanged?.Invoke();
        }
    }

    private void ExpandResultsIfNeeded()
    {
        if (ResultsShell.Visibility == Visibility.Visible)
        {
            return;
        }

        ResultsShell.Visibility = Visibility.Visible;
        LayoutChanged?.Invoke();
    }

    private void CollapseResults()
    {
        ResultsShell.Visibility = Visibility.Collapsed;
        ConfirmPanel.Visibility = Visibility.Collapsed;
        LayoutChanged?.Invoke();
    }

    private void RenderTranscript()
    {
        Transcript.Children.Clear();
        foreach (var line in _lines.TakeLast(12))
        {
            var block = new TextBlock
            {
                Text = line.Kind is "You" or "AI"
                    ? $"{line.Kind}: {line.Text}"
                    : line.Text,
                FontSize = line.Kind is "You" or "AI" ? 13 : 11,
                Opacity = line.Kind is "You" or "AI" ? 1 : 0.8,
                TextWrapping = TextWrapping.WrapWholeWords
            };
            if (_theme is not null)
            {
                if (line.Kind == "Error")
                {
                    WidgetSurfaceStyle.ApplyMuted(block, _theme);
                    block.Foreground = ThemePainter.Brush(_theme.Accent);
                }
                else if (line.Kind == "AI" || line.Kind == "You")
                {
                    WidgetSurfaceStyle.ApplyBody(block, _theme);
                }
                else
                {
                    WidgetSurfaceStyle.ApplyMuted(block, _theme);
                }
            }

            Transcript.Children.Add(block);
        }

        _ = DispatcherQueue.TryEnqueue(() => TranscriptScroll.ChangeView(null, TranscriptScroll.ScrollableHeight, null));
    }
}
