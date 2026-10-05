using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Assistant;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Assistant;

/// <summary>
/// Work-area shelf just above the Windows taskbar: clock, focus, next item, and AI chat.
/// Does not hook Explorer, the taskbar, SearchHost, or Windows Search.
/// </summary>
public sealed partial class TaskbarAiChatBar : UserControl
{
    private IAssistantService? _assistant;
    private Func<AssistantTurnResult, string?>? _applyLaunch;
    private ThemeDefinition? _theme;
    private bool _busy;
    private readonly List<(string Kind, string Text)> _lines = [];

    private Border ResultsShell = null!;
    private ScrollViewer TranscriptScroll = null!;
    private StackPanel Transcript = null!;
    private StackPanel ConfirmPanel = null!;
    private TextBlock ConfirmText = null!;
    private Button ConfirmCancelButton = null!;
    private Button ConfirmRunButton = null!;
    private Button DismissResultsButton = null!;
    private TextBlock StatusLabel = null!;
    private Border Pill = null!;
    private TextBlock ClockText = null!;
    private TextBlock DateText = null!;
    private TextBlock BrandText = null!;
    private TextBlock ProviderText = null!;
    private TextBox InputBox = null!;
    private Button SendButton = null!;
    private TextBlock FocusText = null!;
    private TextBlock NextText = null!;
    private bool _resultsVisible;

    public TaskbarAiChatBar()
    {
        InitializeComponent();
        Content = BuildInterface();
    }

    private Grid BuildInterface()
    {
        ClockText = new TextBlock
        {
            Text = "--:--",
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        DateText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.75
        };
        var clockColumn = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 88,
            Spacing = 0
        };
        clockColumn.Children.Add(ClockText);
        clockColumn.Children.Add(DateText);

        BrandText = new TextBlock
        {
            Text = "Base",
            FontSize = 10,
            CharacterSpacing = 40,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        ProviderText = new TextBlock
        {
            Text = "AI",
            FontSize = 10,
            Opacity = 0.8
        };
        var brandColumn = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 0
        };
        brandColumn.Children.Add(BrandText);
        brandColumn.Children.Add(ProviderText);

        InputBox = new TextBox
        {
            PlaceholderText = "Ask Secret Base…",
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            VerticalAlignment = VerticalAlignment.Center
        };
        InputBox.KeyDown += InputBox_KeyDown;

        SendButton = new Button
        {
            Content = "➤",
            MinWidth = 40,
            MinHeight = 32,
            Padding = new Thickness(8, 4, 8, 4)
        };
        SendButton.Click += SendButton_Click;
        ToolTipService.SetToolTip(SendButton, "Send (Enter)");

        var inputRow = new Grid { ColumnSpacing = 8 };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(brandColumn, 0);
        Grid.SetColumn(InputBox, 1);
        Grid.SetColumn(SendButton, 2);
        inputRow.Children.Add(brandColumn);
        inputRow.Children.Add(InputBox);
        inputRow.Children.Add(SendButton);

        FocusText = new TextBlock
        {
            Text = "Focus idle",
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        NextText = new TextBlock
        {
            Text = "Nothing queued",
            FontSize = 11,
            Opacity = 0.8,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var statusColumn = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 140,
            MaxWidth = 240,
            Spacing = 2
        };
        statusColumn.Children.Add(FocusText);
        statusColumn.Children.Add(NextText);

        var pillRow = new Grid { ColumnSpacing = 16 };
        pillRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pillRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pillRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(clockColumn, 0);
        Grid.SetColumn(inputRow, 1);
        Grid.SetColumn(statusColumn, 2);
        pillRow.Children.Add(clockColumn);
        pillRow.Children.Add(inputRow);
        pillRow.Children.Add(statusColumn);

        Pill = new Border
        {
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            MinHeight = 56,
            Child = pillRow
        };

        Transcript = new StackPanel { Spacing = 6 };
        TranscriptScroll = new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = Transcript
        };

        ConfirmText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        ConfirmCancelButton = new Button { Content = "Cancel", MinWidth = 80 };
        ConfirmCancelButton.Click += ConfirmCancel_Click;
        ConfirmRunButton = new Button { Content = "Run", MinWidth = 88 };
        ConfirmRunButton.Click += ConfirmRun_Click;
        var confirmButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        confirmButtons.Children.Add(ConfirmCancelButton);
        confirmButtons.Children.Add(ConfirmRunButton);
        ConfirmPanel = new StackPanel
        {
            Spacing = 6,
            Visibility = Visibility.Collapsed
        };
        ConfirmPanel.Children.Add(ConfirmText);
        ConfirmPanel.Children.Add(confirmButtons);

        StatusLabel = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.8,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        DismissResultsButton = new Button
        {
            Content = "✕",
            MinWidth = 32,
            MinHeight = 28,
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        DismissResultsButton.Click += DismissResultsButton_Click;
        ToolTipService.SetToolTip(DismissResultsButton, "Dismiss chat (Esc)");

        var resultsHeader = new Grid();
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resultsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerLabel = new TextBlock
        {
            Text = "Base AI",
            FontSize = 11,
            Opacity = 0.75,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerLabel, 0);
        Grid.SetColumn(DismissResultsButton, 1);
        resultsHeader.Children.Add(headerLabel);
        resultsHeader.Children.Add(DismissResultsButton);

        var resultsGrid = new Grid { RowSpacing = 8 };
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(resultsHeader, 0);
        Grid.SetRow(TranscriptScroll, 1);
        Grid.SetRow(ConfirmPanel, 2);
        Grid.SetRow(StatusLabel, 3);
        resultsGrid.Children.Add(resultsHeader);
        resultsGrid.Children.Add(TranscriptScroll);
        resultsGrid.Children.Add(ConfirmPanel);
        resultsGrid.Children.Add(StatusLabel);

        ResultsShell = new Border
        {
            Visibility = Visibility.Collapsed,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Child = resultsGrid
        };
        ResultsShell.SizeChanged += (_, _) => NotifyLayoutChanged();

        var root = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinWidth = 520,
            RowSpacing = 8
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(ResultsShell, 0);
        Grid.SetRow(Pill, 1);
        root.Children.Add(ResultsShell);
        root.Children.Add(Pill);
        return root;
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
        WidgetSurfaceStyle.ApplyHeader(ClockText, DateText, theme);
        ClockText.FontSize = 20;
        ClockText.CharacterSpacing = 0;
        DateText.CharacterSpacing = 0;
        WidgetSurfaceStyle.ApplyBody(FocusText, theme);
        WidgetSurfaceStyle.ApplyMuted(NextText, theme);
        WidgetSurfaceStyle.ApplyMuted(StatusLabel, theme);
        WidgetSurfaceStyle.ApplyBody(ConfirmText, theme);
        InputBox.FontFamily = new FontFamily(theme.FontFamily);
        InputBox.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        WidgetSurfaceStyle.ApplyActionButton(SendButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyActionButton(ConfirmRunButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ConfirmCancelButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(DismissResultsButton, theme);
        RefreshProvider();
        RenderTranscript();
        SyncResultsVisibility();
    }

    public void ApplyShelf(TaskbarShelfSnapshot snapshot)
    {
        ClockText.Text = snapshot.Clock;
        DateText.Text = snapshot.Date;
        FocusText.Text = snapshot.Focus;
        NextText.Text = snapshot.Next;
        RefreshProvider();
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
            DismissResults(clearTranscript: true);
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
            SyncResultsVisibility();
        }
    }

    private void DismissResultsButton_Click(object sender, RoutedEventArgs e) =>
        DismissResults(clearTranscript: true);

    private void ExpandResultsIfNeeded()
    {
        _resultsVisible = true;
        SyncResultsVisibility();
    }

    private void CollapseResults()
    {
        DismissResults(clearTranscript: false);
    }

    private void DismissResults(bool clearTranscript)
    {
        if (_assistant?.HasPendingConfirmation == true)
        {
            _resultsVisible = true;
            StatusLabel.Text = AssistantUserMessages.PendingConfirmationMustResolve;
            SyncResultsVisibility();
            return;
        }

        if (clearTranscript)
        {
            _lines.Clear();
            Transcript.Children.Clear();
            StatusLabel.Text = string.Empty;
        }

        ConfirmPanel.Visibility = Visibility.Collapsed;
        _resultsVisible = false;
        SyncResultsVisibility();
    }

    private void SyncResultsVisibility()
    {
        var hasContent = _lines.Count > 0
            || ConfirmPanel.Visibility == Visibility.Visible
            || _busy
            || !string.IsNullOrWhiteSpace(StatusLabel.Text);

        var shouldShow = _resultsVisible && hasContent;
        if (!shouldShow && !_busy && ConfirmPanel.Visibility != Visibility.Visible)
        {
            _resultsVisible = false;
        }

        var next = shouldShow || (_busy && _resultsVisible) || ConfirmPanel.Visibility == Visibility.Visible
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (ResultsShell.Visibility != next)
        {
            ResultsShell.Visibility = next;
            NotifyLayoutChanged();
            return;
        }

        NotifyLayoutChanged();
    }

    private void NotifyLayoutChanged()
    {
        LayoutChanged?.Invoke();
        // Second pass after measure so SetWindowRgn matches the expanded/collapsed shelf.
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            LayoutChanged?.Invoke());
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

        if (_lines.Count > 0 || ConfirmPanel.Visibility == Visibility.Visible)
        {
            _resultsVisible = true;
        }

        SyncResultsVisibility();
        _ = DispatcherQueue.TryEnqueue(() => TranscriptScroll.ChangeView(null, TranscriptScroll.ScrollableHeight, null));
    }
}
