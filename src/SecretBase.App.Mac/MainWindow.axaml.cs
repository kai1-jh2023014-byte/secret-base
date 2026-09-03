using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SecretBase.Core;
using SecretBase.Core.Assistant;
using SecretBase.Core.Automation;
using SecretBase.Core.Base;
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
        Closing += (_, _) => _session.Logger.Info("lifecycle", "Safe exit — process end only.");

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
        _session.Base.EvaluateAutomation(AutomationTriggerKind.Startup);
        RefreshDashboard();
    }

    private void RefreshClock()
    {
        var (time, date) = ClockDisplayFormatter.Format(_session.TimeProvider, _clock);
        ClockTimeText.Text = time;
        ClockDateText.Text = date.Replace('\n', ' ');
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
            _session.Base.ListUpcomingEvents().Count);
        GreetingText.Text = card.Greeting;
        ReadyText.Text = card.ReadyLine;
        ProjectText.Text = string.IsNullOrWhiteSpace(card.ContinuationTitle)
            ? "Nothing prepared yet"
            : card.ContinuationTitle;
        SessionText.Text = card.ContinuationDetail;
        SuggestionText.Text = string.IsNullOrWhiteSpace(card.SuggestionTitle)
            ? string.Empty
            : card.SuggestionTitle + (string.IsNullOrWhiteSpace(card.SuggestionDetail)
                ? string.Empty
                : " " + card.SuggestionDetail);
        MetaText.Text = $"Calendar {card.CalendarLine} · Tasks {card.TasksLine} · {card.AiLine}";
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
        foreach (var message in _session.Assistant.VisibleHistory)
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

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e) => _session.SafeExit.RequestExit();

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Q && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _session.SafeExit.RequestExit();
        }
    }
}
