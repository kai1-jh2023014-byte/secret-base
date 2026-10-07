using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    private DispatcherTimer? _timer;
    private int _refreshGeneration;
    private bool _disposed;
    private bool _refreshInFlight;

    public ProgressWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        IProgressGenesisProvider provider,
        ProgressWidgetConfiguration? configuration = null,
        ITimeProvider? timeProvider = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _configuration = configuration ?? ProgressWidgetConfiguration.CreateDefault();
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        ApplyTimerInterval();
        _ = RefreshAsync();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
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
        WidgetSurfaceStyle.ApplyProgress(ProgressBar, theme);
        WidgetSurfaceStyle.ApplyProgress(GenesisBar, theme);
        Divider.Background = ThemePainter.Brush(theme.Border, 0.35);
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
        if (_theme is not null)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
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
        }
        finally
        {
            _refreshInFlight = false;
        }
    }

    private void ApplySnapshot(ProgressGenesisSnapshot snapshot)
    {
        snapshot.Normalize();
        var now = _timeProvider.GetLocalNow();

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

        if (_configuration.ShowMilestones && snapshot.Genesis.Milestones.Count > 0)
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
