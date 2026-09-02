using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Clock;

/// <summary>Clock mini-app with multiple display styles and in-widget settings.</summary>
public sealed partial class ClockWidgetView : UserControl, IDisposable
{
    private readonly DispatcherTimer _timer;
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private ClockWidgetConfiguration _configuration = ClockWidgetConfiguration.CreateDefault();
    private Action<ClockWidgetConfiguration>? _onConfigurationChanged;
    private bool _disposed;

    public ClockWidgetView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        ClockWidgetConfiguration configuration,
        ITimeProvider? timeProvider = null,
        Action<ClockWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _onConfigurationChanged = onConfigurationChanged;
        RefreshDisplay();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        WidgetSurfaceStyle.ApplyChrome(RootBorder, theme);
        TimeText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        DateText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        TimeText.FontFamily = new FontFamily(theme.FontFamily);
        DateText.FontFamily = new FontFamily(theme.FontFamily);
        AnalogFace.Stroke = ThemePainter.Brush(theme.Border, 0.9);
        HourHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        MinuteHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        AnalogCenter.Fill = ThemePainter.Brush(theme.Accent);
        WidgetSurfaceStyle.ApplyActionButton(SettingsButton, theme);
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
        var now = _timeProvider.GetLocalNow();
        var scale = Math.Clamp(_configuration.SizeScale, 0.75, 1.5);
        var isAnalog = string.Equals(_configuration.DisplayStyle, ClockWidgetConfiguration.StyleAnalog, StringComparison.Ordinal);
        DigitalPanel.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;
        AnalogPanel.Visibility = isAnalog ? Visibility.Visible : Visibility.Collapsed;

        if (isAnalog)
        {
            UpdateAnalogHands(now);
            return;
        }

        var (time, date) = ClockDisplayFormatter.Format(_timeProvider, _configuration);
        TimeText.Text = time;
        DateText.Text = date;
        DateText.Visibility = string.IsNullOrEmpty(date) ? Visibility.Collapsed : Visibility.Visible;

        TimeText.FontSize = _configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleMinimal => 52 * scale,
            ClockWidgetConfiguration.StyleFocus => 36 * scale,
            _ => 40 * scale
        };
        DateText.FontSize = _configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleMinimal => 14 * scale,
            ClockWidgetConfiguration.StyleFocus => 15 * scale,
            _ => 16 * scale
        };
        DateText.Opacity = _configuration.DisplayStyle == ClockWidgetConfiguration.StyleMinimal ? 0.75 : 1.0;
    }

    private void UpdateAnalogHands(DateTimeOffset now)
    {
        var hour = now.Hour % 12 + now.Minute / 60.0;
        var minute = now.Minute + now.Second / 60.0;
        SetHand(HourHand, hour * 30, 26);
        SetHand(MinuteHand, minute * 6, 38);
    }

    private static void SetHand(Line hand, double angleDegrees, double length)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        hand.X2 = 60 + Math.Sin(radians) * length;
        hand.Y2 = 60 - Math.Cos(radians) * length;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var styleBox = new ComboBox
        {
            Header = "Style",
            ItemsSource = new[] { "Digital", "Minimal", "Analog", "Focus" },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        styleBox.SelectedItem = _configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleMinimal => "Minimal",
            ClockWidgetConfiguration.StyleAnalog => "Analog",
            ClockWidgetConfiguration.StyleFocus => "Focus",
            _ => "Digital"
        };

        var format24 = new CheckBox { Content = "24-hour format", IsChecked = _configuration.Use24HourFormat };
        var showSeconds = new CheckBox { Content = "Show seconds", IsChecked = _configuration.ShowSeconds };
        var showDate = new CheckBox { Content = "Show date", IsChecked = _configuration.ShowDate };
        var sizeBox = new Slider
        {
            Header = "Size",
            Minimum = 0.75,
            Maximum = 1.5,
            StepFrequency = 0.05,
            Value = _configuration.SizeScale
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(styleBox);
        panel.Children.Add(format24);
        panel.Children.Add(showSeconds);
        panel.Children.Add(showDate);
        panel.Children.Add(sizeBox);

        var dialog = new ContentDialog
        {
            Title = "Clock",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _configuration.DisplayStyle = (styleBox.SelectedItem as string) switch
        {
            "Minimal" => ClockWidgetConfiguration.StyleMinimal,
            "Analog" => ClockWidgetConfiguration.StyleAnalog,
            "Focus" => ClockWidgetConfiguration.StyleFocus,
            _ => ClockWidgetConfiguration.StyleDigital
        };
        _configuration.Use24HourFormat = format24.IsChecked == true;
        _configuration.ShowSeconds = showSeconds.IsChecked == true;
        _configuration.ShowDate = showDate.IsChecked == true;
        _configuration.SizeScale = sizeBox.Value;
        _onConfigurationChanged?.Invoke(_configuration);
        RefreshDisplay();
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
