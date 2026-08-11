using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Clock;

/// <summary>
/// Reference widget view: local-only clock. No network/file/process access.
/// </summary>
public sealed partial class ClockWidgetView : UserControl, IDisposable
{
    private readonly DispatcherTimer _timer;
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private ClockWidgetConfiguration _configuration = ClockWidgetConfiguration.CreateDefault();
    private bool _disposed;

    public ClockWidgetView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(ClockWidgetConfiguration configuration, ITimeProvider? timeProvider = null)
    {
        _configuration = configuration;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        // v0.1 product requirement: fixed 24-hour display.
        _configuration.Use24HourFormat = true;
        RefreshDisplay();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        TimeText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        DateText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        TimeText.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(theme.FontFamily);
        DateText.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(theme.FontFamily);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        RefreshDisplay();
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => StopTimer();

    private void OnTimerTick(object? sender, object e) => RefreshDisplay();

    private void RefreshDisplay()
    {
        var (time, date) = ClockDisplayFormatter.Format(_timeProvider, _configuration);
        TimeText.Text = time;
        DateText.Text = date;
        DateText.Visibility = string.IsNullOrEmpty(date) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void StopTimer()
    {
        if (_timer.IsEnabled)
        {
            _timer.Stop();
        }
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
        _timer.Tick -= OnTimerTick;
        StopTimer();
    }
}
