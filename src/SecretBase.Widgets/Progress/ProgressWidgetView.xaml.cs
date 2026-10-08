using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Progress;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Progress;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Progress;

/// <summary>Desktop surface for Progress + Genesis advancement.</summary>
public sealed partial class ProgressWidgetView : UserControl, IDisposable
{
    private IProgressGenesisProvider? _provider;
    private ProgressWidgetConfiguration _configuration = ProgressWidgetConfiguration.CreateDefault();
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private ThemeDefinition? _theme;
    private ProgressGenesisSnapshot? _lastSnapshot;
    private DispatcherTimer? _timer;
    private int _refreshGeneration;
    private bool _disposed;
    private bool _refreshInFlight;
    private Action? _onConfigurationChanged;

    public ProgressWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        IProgressGenesisProvider provider,
        ProgressWidgetConfiguration? configuration = null,
        ITimeProvider? timeProvider = null,
        Action? onConfigurationChanged = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _configuration = configuration ?? ProgressWidgetConfiguration.CreateDefault();
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _onConfigurationChanged = onConfigurationChanged;
        ApplyTimerInterval();
        ApplyDisplayMode();
        _ = RefreshAsync();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        ApplySurface(theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SubtitleText, theme);
        WidgetSurfaceStyle.ApplyBody(ProgressTitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(ProgressStatusText, theme);
        WidgetSurfaceStyle.ApplyMuted(ProgressDetailText, theme);
        WidgetSurfaceStyle.ApplyBody(GenesisTitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(GenesisStatusText, theme);
        WidgetSurfaceStyle.ApplyMuted(GenesisPercentText, theme);
        WidgetSurfaceStyle.ApplyMuted(MilestoneText, theme);
        WidgetSurfaceStyle.ApplyMuted(UpdatedText, theme);
        WidgetSurfaceStyle.ApplyMuted(SourceText, theme);
        WidgetSurfaceStyle.ApplyGhostButton(RefreshButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(ModeButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(MinimalModeButton, theme);
        WidgetSurfaceStyle.ApplyProgress(ProgressBar, theme);
        WidgetSurfaceStyle.ApplyProgress(GenesisBar, theme);
        WidgetSurfaceStyle.ApplyProgress(MinimalProgressBar, theme);
        WidgetSurfaceStyle.ApplyProgress(MinimalGenesisBar, theme);
        WidgetSurfaceStyle.ApplyMuted(MinimalProgressLabel, theme);
        WidgetSurfaceStyle.ApplyMuted(MinimalGenesisLabel, theme);
        WidgetSurfaceStyle.ApplyBody(MinimalProgressText, theme);
        WidgetSurfaceStyle.ApplyBody(MinimalGenesisText, theme);
        Divider.Background = ThemePainter.Brush(theme.Border, 0.35);
        ModeButton.Content = _configuration.IsMinimal ? "◆" : "◇";
        ToolTipService.SetToolTip(
            ModeButton,
            _configuration.IsMinimal ? "Full card mode" : "Minimal transparent mode");
    }

    private void ApplySurface(ThemeDefinition theme)
    {
        if (_configuration.IsMinimal)
        {
            OuterShell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            OuterShell.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            OuterShell.BorderThickness = new Thickness(0);
            OuterShell.Padding = new Thickness(0);
            RootBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            RootBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.Padding = new Thickness(10, 8, 10, 8);
            RootBorder.MinHeight = 96;
            RootBorder.MinWidth = 180;
            return;
        }

        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        RootBorder.MinHeight = 160;
        RootBorder.MinWidth = 200;
    }

    private void ApplyDisplayMode()
    {
        var minimal = _configuration.IsMinimal;
        FullPanel.Visibility = minimal ? Visibility.Collapsed : Visibility.Visible;
        MinimalPanel.Visibility = minimal ? Visibility.Visible : Visibility.Collapsed;
        if (_theme is not null)
        {
            ApplyTheme(_theme);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        WidgetSurfaceStyle.FadeOpacity(this, 1, _theme?.MotionDurationMs ?? 220);
        EnsureTimer();
        _ = RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopTimer();

    private void EnsureTimer()
    {
        _timer ??= new DispatcherTimer();
        _timer.Tick -= Timer_Tick;
        _timer.Tick += Timer_Tick;
        ApplyTimerInterval();
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void ApplyTimerInterval()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Interval = TimeSpan.FromSeconds(
            ProgressWidgetConfiguration.ClampRefreshSeconds(_configuration.RefreshSeconds));
    }

    private void Timer_Tick(object? sender, object e) => _ = RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        if (_theme is not null && !_configuration.IsMinimal)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
    }

    private void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        _configuration.DisplayMode = _configuration.IsMinimal
            ? ProgressWidgetConfiguration.DisplayFull
            : ProgressWidgetConfiguration.DisplayMinimal;
        ApplyDisplayMode();
        if (_lastSnapshot is not null)
        {
            ApplySnapshot(_lastSnapshot);
        }

        _onConfigurationChanged?.Invoke();
    }

    private async Task RefreshAsync()
    {
        if (_provider is null || _disposed || _refreshInFlight)
        {
            return;
        }

        _refreshInFlight = true;
        var generation = ++_refreshGeneration;
        try
        {
            var snapshot = await _provider.GetAsync();
            if (_disposed || generation != _refreshGeneration)
            {
                return;
            }

            ApplySnapshot(snapshot);
        }
        catch
        {
            if (_disposed || generation != _refreshGeneration)
            {
                return;
            }

            UpdatedText.Text = "Refresh failed";
            MinimalProgressText.Text = "—";
            MinimalGenesisText.Text = "—";
            MinimalProgressBar.Value = 0;
            MinimalGenesisBar.Value = 0;
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private void ApplySnapshot(ProgressGenesisSnapshot snapshot)
    {
        snapshot.Normalize();
        _lastSnapshot = snapshot;
        var now = _timeProvider.GetLocalNow();

        MinimalProgressLabel.Text = "Progress";
        MinimalProgressText.Text = ProgressGenesisFormatter.FormatMinimalProgressPercent(snapshot.Progress);
        MinimalProgressBar.Value = snapshot.Progress.Percent;
        MinimalGenesisLabel.Text = ProgressGenesisFormatter.FormatMinimalGenesisLabel(snapshot.Genesis);
        MinimalGenesisText.Text = ProgressGenesisFormatter.FormatMinimalGenesisPercent(snapshot.Genesis);
        MinimalGenesisBar.Value = snapshot.Genesis.Percent;

        ProgressTitleText.Text = ProgressGenesisFormatter.FormatProgressHeadline(snapshot.Progress);
        ProgressStatusText.Text = snapshot.Progress.Status;
        ProgressBar.Value = snapshot.Progress.Percent;
        ProgressDetailText.Text = snapshot.Progress.Detail ?? string.Empty;
        ProgressDetailText.Visibility = string.IsNullOrWhiteSpace(snapshot.Progress.Detail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        GenesisTitleText.Text = ProgressGenesisFormatter.FormatGenesisHeadline(snapshot.Genesis);
        GenesisStatusText.Text = snapshot.Genesis.Status;
        GenesisBar.Value = snapshot.Genesis.Percent;
        GenesisPercentText.Text = ProgressGenesisFormatter.FormatGenesisPercentLine(snapshot.Genesis);

        if (_configuration.ShowMilestones && !_configuration.IsMinimal && snapshot.Genesis.Milestones.Count > 0)
        {
            var next = snapshot.Genesis.Milestones.FirstOrDefault(m => !m.IsComplete);
            MilestoneText.Text = next is null
                ? "All Genesis milestones complete"
                : "Next · " + next.Label;
            MilestoneText.Visibility = Visibility.Visible;
        }
        else
        {
            MilestoneText.Text = string.Empty;
            MilestoneText.Visibility = Visibility.Collapsed;
        }

        UpdatedText.Text = ProgressGenesisFormatter.FormatUpdatedAt(snapshot.UpdatedAt, now);
        SourceText.Text = ProgressGenesisFormatter.FormatSourceCaption(snapshot.SourceKind);
        SubtitleText.Text = string.IsNullOrWhiteSpace(snapshot.Genesis.Phase)
            ? "Progress + Genesis"
            : $"Genesis · {snapshot.Genesis.Phase}";
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        StopTimer();
    }
}
