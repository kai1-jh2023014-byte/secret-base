using System.Text;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SecretBase.Core;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
using SecretBase.Core.Capture;
using SecretBase.Core.Commands;
using SecretBase.Core.Connectors;
using SecretBase.Core.Memory;
using SecretBase.Core.Workspace;
using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Infrastructure.Startup;
using SecretBase.Platform.Abstractions;
using SecretBase.Platform.Mac;

namespace SecretBase.App.Mac;

public partial class MainWindow : Window
{
    private MacHostSession _session = null!;
    private DispatcherTimer? _clockTimer;
    private readonly AvaloniaPathPickService _pathPicker = new();
    private DesktopLayout? _layout;
    private ThemeDefinition? _theme;
    private ClockWidgetConfiguration _clock = ClockWidgetConfiguration.CreateDefault();
    private bool _busy;
    private bool _autoStartSync;

    private int _clockTicks;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MacHostSession session) : this()
    {
        _session = session;
        _pathPicker.SetOwner(this);

        KeyDown += OnWindowKeyDown;
        Opened += (_, _) =>
        {
            _session.Logger.Info("overlay", "macOS workspace window activated. Not a Dock/Finder replacement.");
            RefreshClock();
            RefreshProvider();
            RefreshDashboard();
            InitializeAutoStartToggle();
            RefreshHistory();
        };
        Closing += (_, _) =>
        {
            try
            {
                _session.Base.Sessions.End(_session.TimeProvider.GetLocalNow());
            }
            catch (Exception)
            {
                // Session persist must never block safe exit.
            }

            _session.Logger.Info("lifecycle", "Safe exit — process end only.");
        };

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) =>
        {
            RefreshClock();
            _clockTicks++;
            if (_clockTicks % 15 == 0)
            {
                RefreshDashboard();
            }
        };
        _clockTimer.Start();

        LoadWorkspace();
    }

    private void LoadWorkspace()
    {
        _theme = _session.ThemeStore.LoadOrCreateDefault();
        _layout = _session.LayoutStore.LoadOrCreateDefault(RoomId.DefaultRoomId);
        var clockWidget = _layout.Widgets.FirstOrDefault(w => w.Type == WidgetTypes.Clock);
        if (clockWidget is not null)
        {
            _clock = ClockWidgetConfiguration.FromDictionary(clockWidget.Configuration);
        }

        TitleText.Text = string.IsNullOrWhiteSpace(_theme.DisplayName) ? AppInfo.Name : _theme.DisplayName;
        StatusText.Text = $"v{AppInfo.Version} · {_session.Compatibility.OsDescription}";
        ApplyAppearance();
        _session.Base.EvaluateAutomation(AutomationTriggerKind.Startup);
        RefreshDashboard();
    }

    private void RefreshClock()
    {
        var status = ClockBaseStatusComposer.Compose(
            _session.Base.Now,
            _session.Base.ListUpcomingEvents(),
            _session.Base.CurrentWorkspace,
            _session.Base.Focus.Current,
            _session.Assistant.ProviderStatus);
        var (time, date, next, statusLine) = ClockDisplayFormatter.FormatBase(_session.TimeProvider, _clock, status);
        ClockTimeText.Text = time;
        ClockDateText.Text = date.Replace('\n', ' ');
        ClockNextText.Text = string.IsNullOrWhiteSpace(next) ? statusLine : next;
    }

    private void RefreshProvider()
    {
        ProviderText.Text = BaseAiStatusFormatter.Format(_session.Assistant.ProviderStatus);
        RefreshDashboard();
    }

    private void RefreshDashboard()
    {
        var state = _session.Base.ComposeUserState(provider: _session.Assistant.ProviderStatus);
        var continuation = _session.Base.Continuation();
        var card = BaseDashboardComposer.Compose(
            state,
            continuation,
            _session.Base.LastSuggestion,
            _session.Base.ListUpcomingEvents().Count,
            _session.Base.ComposeSituation());
        GreetingText.Text = card.Greeting;
        ReadyText.Text = card.ReadyLine;
        ProjectText.Text = string.IsNullOrWhiteSpace(card.ContinuationTitle)
            ? UxCopy.FirstLine(UxCopy.WorkspaceEmpty)
            : card.ContinuationTitle;
        SessionText.Text = string.Join(
            Environment.NewLine,
            new[] { card.ContinuationDetail, card.NextTaskLine }
                .Where(line => !string.IsNullOrWhiteSpace(line)));
        SuggestionText.Text = string.IsNullOrWhiteSpace(card.SuggestionTitle)
            ? string.Empty
            : card.SuggestionTitle + (string.IsNullOrWhiteSpace(card.SuggestionDetail)
                ? string.Empty
                : " " + card.SuggestionDetail);
        MetaText.Text = $"Calendar {card.CalendarLine} · Tasks {card.TasksLine} · {card.AiLine}"
                        + (string.IsNullOrWhiteSpace(card.AttentionLine) ? string.Empty : " · " + card.AttentionLine);
        ContinueButton.IsEnabled = card.ShowContinue || !string.IsNullOrWhiteSpace(card.SuggestionTitle);
        NotNowButton.IsVisible = !string.IsNullOrWhiteSpace(card.SuggestionTitle);
    }

    private void ContinueButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var session = _session.Base.CurrentWorkspace
                      ?? WorkspacePreparer.Prepare(
                          "Continue",
                          _session.Base.ListProjects(),
                          _session.Base.ListApps(),
                          _session.Base.LoadTodos(),
                          _session.Base.ListUpcomingEvents(),
                          _session.Base.Now,
                          _session.Base.Memory.Recall(_session.Base.Now));
        _session.Base.RememberPreparedWorkspace(session);
        _session.Base.RecordFeedback(true);
        StatusText.Text =
            "Workspace ready. Opening a registered project still requires confirmation in Base AI.";
        RefreshDashboard();
    }

    private void NotNowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _session.Base.RecordFeedback(false);
        _session.Base.LastSuggestion = null;
        RefreshDashboard();
    }

    private void InitializeAutoStartToggle()
    {
        _autoStartSync = true;
        if (!_session.AutoStart.IsSupported)
        {
            AutoStartToggle.IsVisible = false;
            _autoStartSync = false;
            return;
        }

        var settings = _session.LaunchSettings.LoadOrCreate();
        var status = _session.AutoStart.GetStatus();
        AutoStartToggle.IsChecked = settings.LaunchAtWindowsLogin && status.PointsToCurrentExecutable;
        AutoStartToggle.IsEnabled = _session.AutoStart.TryGetStartupExecutablePath(out _, out _) || status.IsRegistered;
        AutoStartToggle.IsCheckedChanged += AutoStartToggle_OnChanged;
        _autoStartSync = false;
    }

    private void AutoStartToggle_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_autoStartSync)
        {
            return;
        }

        var desired = AutoStartToggle.IsChecked == true;
        _autoStartSync = true;
        if (!AutoStartCoordinator.TrySetEnabled(
                desired,
                _session.AutoStart,
                _session.LaunchSettings,
                _session.Logger,
                out var error))
        {
            AutoStartToggle.IsChecked = !desired;
            StatusText.Text = error ?? "Auto-start could not be updated.";
        }
        else
        {
            StatusText.Text = desired ? "Will open at login (LaunchAgent)." : "Login auto-start off.";
        }

        _autoStartSync = false;
    }

    private async void SendButton_OnClick(object? sender, RoutedEventArgs e) => await SendAsync();

    private async void InputBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (_busy)
        {
            return;
        }

        var text = InputBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (_session.Assistant.HasPendingConfirmation)
        {
            StatusText.Text = AssistantUserMessages.PendingConfirmationMustResolve;
            return;
        }

        InputBox.Text = string.Empty;
        _busy = true;
        SendButton.IsEnabled = false;
        try
        {
            var result = await _session.Assistant.SendAsync(text);
            ApplyTurn(result);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            _busy = false;
            SendButton.IsEnabled = true;
            RefreshProvider();
        }
    }

    private async void ConfirmButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var result = await _session.Assistant.ConfirmPendingAsync();
            ApplyTurn(result);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        try
        {
            var result = await _session.Assistant.ContinueAfterCancelAsync();
            ApplyTurn(result);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ApplyTurn(AssistantTurnResult result)
    {
        RefreshHistory();
        if (result.PendingConfirmation is not null)
        {
            ConfirmPanel.IsVisible = true;
            ConfirmText.Text = result.PendingConfirmation.Prompt;
        }
        else
        {
            ConfirmPanel.IsVisible = false;
            ConfirmText.Text = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            StatusText.Text = result.ErrorMessage;
        }
        else if (!string.IsNullOrWhiteSpace(result.AssistantText))
        {
            StatusText.Text = result.ResponseKind.ToString();
        }

        var launchError = TryApplyLaunch(result);
        if (!string.IsNullOrWhiteSpace(launchError))
        {
            StatusText.Text = launchError;
        }
    }

    private void RefreshHistory()
    {
        var builder = new StringBuilder();
        var history = _session.Assistant.VisibleHistory;
        if (history.Count == 0)
        {
            HistoryText.Text = BaseAiPresence.Format(
                _session.Assistant.ProviderStatus,
                _session.Base.ComposeUserState(),
                _session.Base.ComposeSituation());
            return;
        }

        foreach (var message in history)
        {
            var role = message.Role == AiMessageRole.User ? "You" : "AI";
            builder.AppendLine($"{role}: {message.Content}");
            builder.AppendLine();
        }

        HistoryText.Text = builder.ToString().TrimEnd();
    }

    private string? TryApplyLaunch(AssistantTurnResult result)
    {
        if (result.ShouldOpenCursorAtFolder && !string.IsNullOrWhiteSpace(result.CursorFolderPath))
        {
            var opened = _session.CursorLaunch.TryOpenFolder(result.CursorFolderPath);
            return opened.Succeeded ? null : opened.ErrorMessage;
        }

        if (!result.ShouldLaunch || string.IsNullOrWhiteSpace(result.LaunchTarget))
        {
            return null;
        }

        if (result.LaunchIsExternalLink)
        {
            return MacHttpsLauncher.TryOpen(result.LaunchTarget)
                ? null
                : "Could not open link in the system browser.";
        }

        var path = result.LaunchTarget;
        var exists = File.Exists(path) || Directory.Exists(path);
        if (!exists)
        {
            return "Path was not found.";
        }

        var inferred = BlockTargetValidator.InferType(path, Directory.Exists(path));
        var launched = _session.Launcher.TryLaunch(new TargetLaunchRequest(
            Target: path,
            ItemType: inferred.ToString(),
            DisplayName: HostPath.GetFileName(path)));
        return launched.Succeeded ? null : launched.ErrorMessage;
    }

    private void SaveKeyButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var typed = ApiKeyBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typed))
        {
            _session.Secrets.DeleteSecret(AssistantSecretKeys.OpenAiApiKey);
            StatusText.Text = "OpenAI key removed.";
        }
        else
        {
            _session.Secrets.SetSecret(AssistantSecretKeys.OpenAiApiKey, typed);
            ApiKeyBox.Text = string.Empty;
            StatusText.Text = "OpenAI key saved in the OS secret store.";
        }

        RefreshProvider();
    }

    private void ApplyAppearance()
    {
        if (_theme is null)
        {
            return;
        }

        ThemeMigrator.Normalize(_theme);
        Background = Brush(_theme.Background);
        BrandText.Foreground = Brush(_theme.Accent);
        TitleText.Foreground = Brush(_theme.Foreground);
        SubtitleText.Foreground = Brush(_theme.ForegroundMuted);
        ClockCard.Background = Brush(_theme.WidgetBackground, _theme.Transparency);
        ClockCard.CornerRadius = new Avalonia.CornerRadius(_theme.CornerRadius);
        ClockTimeText.Foreground = Brush(_theme.WidgetForeground);
        ClockTimeText.FontSize = Math.Max(32, _theme.TitleSize);
        ClockDateText.Foreground = Brush(_theme.ForegroundMuted);
        ClockNextText.Foreground = Brush(_theme.ForegroundMuted);
        GreetingText.Foreground = Brush(_theme.Accent);
        DashboardCard.Background = Brush(_theme.WidgetBackground, _theme.Transparency);
        DashboardCard.CornerRadius = new Avalonia.CornerRadius(_theme.CornerRadius);
        ChatCard.Background = Brush(_theme.WidgetBackground, _theme.Transparency);
        ChatCard.CornerRadius = new Avalonia.CornerRadius(_theme.CornerRadius);
        ReadyText.Foreground = Brush(_theme.WidgetForeground);
        ProjectText.Foreground = Brush(_theme.WidgetForeground);
        SessionText.Foreground = Brush(_theme.ForegroundMuted);
        SuggestionText.Foreground = Brush(_theme.Accent);
        MetaText.Foreground = Brush(_theme.ForegroundMuted);
        HistoryText.Foreground = Brush(_theme.WidgetForeground);
        ProviderText.Foreground = Brush(_theme.Accent);
        TitleText.Text = string.IsNullOrWhiteSpace(_theme.DisplayName) ? AppInfo.Name : _theme.DisplayName;
    }

    private static Avalonia.Media.IBrush Brush(string hex, double opacity = 1)
    {
        if (!ThemeColor.TryParse(hex, out var a, out var r, out var g, out var b))
        {
            return new SolidColorBrush(Color.FromRgb(14, 18, 24));
        }

        a = (byte)Math.Clamp((int)Math.Round(a * Math.Clamp(opacity, 0.35, 1)), 0, 255);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    private async void AppearanceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_theme is null)
        {
            return;
        }

        var themeBox = new ComboBox { ItemsSource = ThemePresets.Names.ToList(), SelectedItem = _theme.DisplayName };
        var accentBox = new ComboBox { ItemsSource = AccentPalette.Names.ToList(), SelectedItem = _theme.AccentName };
        var densityBox = new ComboBox { ItemsSource = AppearanceDensity.All.ToList(), SelectedItem = _theme.Density };
        var shapeBox = new ComboBox { ItemsSource = AppearanceShape.All.ToList(), SelectedItem = _theme.Shape };
        var motionBox = new ComboBox { ItemsSource = AppearanceMotion.All.ToList(), SelectedItem = _theme.Motion };
        var apply = new Button { Content = "Apply" };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 };
        var preview = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
        void RefreshPreview()
        {
            preview.Text = new AppearanceProfile
            {
                VisualTheme = themeBox.SelectedItem as string ?? VisualThemeNames.Atelier,
                AccentName = accentBox.SelectedItem as string ?? AccentPalette.Jade,
                Density = densityBox.SelectedItem as string ?? AppearanceDensity.Comfortable,
                Shape = shapeBox.SelectedItem as string ?? AppearanceShape.Balanced,
                Motion = motionBox.SelectedItem as string ?? AppearanceMotion.Subtle
            }.Format();
        }

        themeBox.SelectionChanged += (_, _) => RefreshPreview();
        accentBox.SelectionChanged += (_, _) => RefreshPreview();
        densityBox.SelectionChanged += (_, _) => RefreshPreview();
        shapeBox.SelectionChanged += (_, _) => RefreshPreview();
        motionBox.SelectionChanged += (_, _) => RefreshPreview();
        RefreshPreview();

        panel.Children.Add(new TextBlock { Text = "Your style — applied to this workspace." });
        panel.Children.Add(themeBox);
        panel.Children.Add(accentBox);
        panel.Children.Add(densityBox);
        panel.Children.Add(shapeBox);
        panel.Children.Add(motionBox);
        panel.Children.Add(preview);
        panel.Children.Add(apply);
        var window = new Window
        {
            Title = "Appearance",
            Width = 420,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        apply.Click += (_, _) =>
        {
            AppearanceComposer.Apply(_theme, new AppearanceProfile
            {
                VisualTheme = themeBox.SelectedItem as string ?? VisualThemeNames.Atelier,
                AccentName = accentBox.SelectedItem as string ?? AccentPalette.Jade,
                CustomAccent = _theme.Accent,
                Density = densityBox.SelectedItem as string ?? AppearanceDensity.Comfortable,
                Shape = shapeBox.SelectedItem as string ?? AppearanceShape.Balanced,
                Motion = motionBox.SelectedItem as string ?? AppearanceMotion.Subtle,
                Transparency = _theme.Transparency
            });
            _session.ThemeStore.Save(_theme);
            ApplyAppearance();
            window.Close();
        };
        await window.ShowDialog(this);
    }

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e) => _session.SafeExit.RequestExit();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _session.SafeExit.RequestExit();
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            _ = ShowPaletteAsync();
        }
    }

    private async void PaletteButton_OnClick(object? sender, RoutedEventArgs e) => await ShowPaletteAsync();

    private async void CaptureButton_OnClick(object? sender, RoutedEventArgs e) => await ShowCaptureAsync();

    private async void MemoryButton_OnClick(object? sender, RoutedEventArgs e) => await ShowMemoryAsync();

    private async void PrivacyButton_OnClick(object? sender, RoutedEventArgs e) => await ShowPrivacyAsync();

    private async void IntegrationsButton_OnClick(object? sender, RoutedEventArgs e) => await ShowIntegrationsAsync();

    private async Task ShowPaletteAsync()
    {
        var items = _session.Base.Palette(string.Empty);
        var body = string.Join(Environment.NewLine, items.Take(8).Select(item => item.Title + " — " + item.Subtitle));
        var window = new Window
        {
            Title = "Command palette",
            Width = 520,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = body + Environment.NewLine + Environment.NewLine + "Type the same phrases in Base AI. Continue still confirms.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(16)
                }
            }
        };
        await window.ShowDialog(this);
        StatusText.Text = "Command palette · Ctrl+Space";
    }

    private async Task ShowCaptureAsync()
    {
        var box = new TextBox { Watermark = UxCopy.CapturePlaceholder };
        var destinations = new ComboBox
        {
            ItemsSource = new[] { "Auto", "Idea", "Todo", "Note", "Memory", "Project" },
            SelectedIndex = 0
        };
        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(16) };
        panel.Children.Add(new TextBlock { Text = UxCopy.CapturePrompt });
        panel.Children.Add(box);
        panel.Children.Add(destinations);
        var save = new Button { Content = "Save" };
        panel.Children.Add(save);
        var window = new Window
        {
            Title = "Quick capture",
            Width = 420,
            Height = 280,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        save.Click += (_, _) =>
        {
            var draft = _session.Base.ClassifyCapture(box.Text ?? string.Empty);
            CaptureDestination? force = (destinations.SelectedItem as string) switch
            {
                "Idea" => CaptureDestination.Idea,
                "Todo" => CaptureDestination.Todo,
                "Note" => CaptureDestination.Note,
                "Memory" => CaptureDestination.Memory,
                "Project" => CaptureDestination.Project,
                _ => null
            };
            var saved = _session.Base.CommitCapture(draft, force);
            StatusText.Text = saved is null ? UxCopy.FirstLine(UxCopy.CaptureRefused) : "Saved as " + saved.Kind;
            window.Close();
        };
        await window.ShowDialog(this);
    }

    private async Task ShowMemoryAsync()
    {
        var body = PersonalSpaceCatalog.Memories(_session.Base.Memory, _session.Base.Now);
        await ShowInfoAsync("What does Secret Base remember?", body);
    }

    private async Task ShowPrivacyAsync()
    {
        var body = _session.Base.Privacy()
                   + Environment.NewLine
                   + Environment.NewLine
                   + PersonalSpaceCatalog.Rules(_session.Base.Rules);
        await ShowInfoAsync("Privacy & automation", body);
    }

    private async Task ShowIntegrationsAsync()
    {
        DemoManifests.TryRegisterKnown(_session.Integrations.Registry, approveDemos: true);
        var paste = new TextBox
        {
            AcceptsReturn = true,
            Height = 120,
            Watermark = "Paste secretbase.integration.json to register your app"
        };
        var status = new TextBlock
        {
            Text = _session.Integrations.CatalogText()
                   + Environment.NewLine
                   + Environment.NewLine
                   + _session.Integrations.PermissionText(),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var register = new Button { Content = "Register pasted" };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 };
        panel.Children.Add(new ScrollViewer { Height = 220, Content = status });
        panel.Children.Add(paste);
        panel.Children.Add(register);
        var window = new Window
        {
            Title = "My Integrations",
            Width = 560,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };
        register.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(paste.Text))
            {
                return;
            }

            if (IntegrationDiscovery.TryRegisterJson(_session.Integrations.Registry, paste.Text, approved: true, out var error))
            {
                status.Text = _session.Integrations.CatalogText()
                              + Environment.NewLine
                              + Environment.NewLine
                              + _session.Integrations.PermissionText();
                paste.Text = string.Empty;
            }
            else
            {
                status.Text = error;
            }
        };
        await window.ShowDialog(this);
    }

    private async Task ShowInfoAsync(string title, string body)
    {
        var window = new Window
        {
            Title = title,
            Width = 560,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = body,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(16)
                }
            }
        };
        await window.ShowDialog(this);
    }
}
