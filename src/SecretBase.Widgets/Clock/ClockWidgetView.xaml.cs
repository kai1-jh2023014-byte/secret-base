using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Clock;

/// <summary>Atelier Clock — editorial digital / analog / focus / minimal / base styles.</summary>
public sealed partial class ClockWidgetView : UserControl, IDisposable
{
    private readonly DispatcherTimer _timer;
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private ClockWidgetConfiguration _configuration = ClockWidgetConfiguration.CreateDefault();
    private Action<ClockWidgetConfiguration>? _onConfigurationChanged;
    private Func<ClockBaseStatus?>? _statusSource;
    private ThemeDefinition? _theme;
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
        Action<ClockWidgetConfiguration>? onConfigurationChanged = null,
        Func<ClockBaseStatus?>? statusSource = null)
    {
        _configuration = configuration;
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _onConfigurationChanged = onConfigurationChanged;
        _statusSource = statusSource;
        RefreshDisplay();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        TimeText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        DateText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusText.Foreground = ThemePainter.Brush(theme.Accent);
        NextText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        TimeText.FontFamily = new FontFamily(theme.FontFamily);
        TimeText.FontSize = Math.Max(28, theme.TitleSize);
        DateText.FontFamily = new FontFamily(theme.FontFamily);
        StatusText.FontFamily = new FontFamily(theme.FontFamily);
        NextText.FontFamily = new FontFamily(theme.FontFamily);
        AccentHairline.Background = ThemePainter.Brush(theme.Accent, 0.9);
        AnalogRingOuter.Stroke = ThemePainter.Brush(theme.Border, 0.35);
        AnalogFace.Stroke = ThemePainter.Brush(theme.Border, 0.7);
        HourHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        MinuteHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        SecondHand.Stroke = ThemePainter.Brush(theme.Accent, 0.95);
        AnalogCenter.Fill = ThemePainter.Brush(theme.Accent);
        WidgetSurfaceStyle.ApplyGhostButton(SettingsButton, theme);
        RefreshDisplay();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        RefreshDisplay();
        WidgetSurfaceStyle.FadeOpacity(this, 1, _theme);
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
        var style = _configuration.DisplayStyle ?? ClockWidgetConfiguration.StyleDigital;
        var isAnalog = string.Equals(style, ClockWidgetConfiguration.StyleAnalog, StringComparison.Ordinal);
        var isBase = string.Equals(style, ClockWidgetConfiguration.StyleBase, StringComparison.Ordinal);
        DigitalPanel.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;
        AnalogPanel.Visibility = isAnalog ? Visibility.Visible : Visibility.Collapsed;
        AccentHairline.Visibility = style is ClockWidgetConfiguration.StyleMinimal
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (isAnalog)
        {
            UpdateAnalogHands(now);
            StatusText.Visibility = Visibility.Collapsed;
            NextText.Visibility = Visibility.Collapsed;
            return;
        }

        if (isBase)
        {
            var status = _statusSource?.Invoke();
            var (time, date, next, statusLine) = ClockDisplayFormatter.FormatBase(
                _timeProvider,
                _configuration,
                status);
            TimeText.Text = time;
            DateText.Text = date;
            DateText.Visibility = string.IsNullOrEmpty(date) ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Text = statusLine;
            StatusText.Visibility = string.IsNullOrWhiteSpace(statusLine) ? Visibility.Collapsed : Visibility.Visible;
            NextText.Text = next;
            NextText.Visibility = string.IsNullOrWhiteSpace(next) ? Visibility.Collapsed : Visibility.Visible;
        }
        else
        {
            var (time, date) = ClockDisplayFormatter.Format(_timeProvider, _configuration);
            TimeText.Text = time;
            DateText.Text = date;
            DateText.Visibility = string.IsNullOrEmpty(date) ? Visibility.Collapsed : Visibility.Visible;
            StatusText.Visibility = Visibility.Collapsed;
            NextText.Visibility = Visibility.Collapsed;
        }

        TimeText.FontSize = style switch
        {
            ClockWidgetConfiguration.StyleMinimal => 56 * scale,
            ClockWidgetConfiguration.StyleFocus => 38 * scale,
            ClockWidgetConfiguration.StyleBase => 42 * scale,
            _ => 44 * scale
        };
        TimeText.FontWeight = style == ClockWidgetConfiguration.StyleMinimal
            ? Microsoft.UI.Text.FontWeights.Light
            : Microsoft.UI.Text.FontWeights.SemiLight;
        TimeText.CharacterSpacing = style == ClockWidgetConfiguration.StyleMinimal ? 120 : 80;
        TimeText.HorizontalAlignment = style == ClockWidgetConfiguration.StyleMinimal
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        DateText.HorizontalAlignment = TimeText.HorizontalAlignment;
        StatusText.HorizontalAlignment = TimeText.HorizontalAlignment;
        NextText.HorizontalAlignment = TimeText.HorizontalAlignment;
        DateText.FontSize = style switch
        {
            ClockWidgetConfiguration.StyleMinimal => 12 * scale,
            ClockWidgetConfiguration.StyleFocus => 14 * scale,
            _ => 13 * scale
        };
        DateText.Opacity = style == ClockWidgetConfiguration.StyleMinimal ? 0.7 : 0.92;
    }

    private void UpdateAnalogHands(DateTimeOffset now)
    {
        var hour = now.Hour % 12 + now.Minute / 60.0;
        var minute = now.Minute + now.Second / 60.0;
        var second = now.Second + now.Millisecond / 1000.0;
        SetHand(HourHand, hour * 30, 26);
        SetHand(MinuteHand, minute * 6, 38);
        SetHand(SecondHand, second * 6, 42);
        SecondHand.Visibility = _configuration.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetHand(Line hand, double angleDegrees, double length)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        hand.X2 = 66 + Math.Sin(radians) * length;
        hand.Y2 = 66 - Math.Cos(radians) * length;
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var styleBox = new ComboBox
        {
            Header = "Style",
            ItemsSource = new[] { "Base", "Digital", "Minimal", "Analog", "Focus" },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        styleBox.SelectedItem = _configuration.DisplayStyle switch
        {
            ClockWidgetConfiguration.StyleMinimal => "Minimal",
            ClockWidgetConfiguration.StyleAnalog => "Analog",
            ClockWidgetConfiguration.StyleFocus => "Focus",
            ClockWidgetConfiguration.StyleBase => "Base",
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

        var panel = new StackPanel { Spacing = 10 };
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
            "Base" => ClockWidgetConfiguration.StyleBase,
            _ => ClockWidgetConfiguration.StyleDigital
        };
        _configuration.Use24HourFormat = format24.IsChecked == true;
        _configuration.ShowSeconds = showSeconds.IsChecked == true;
        _configuration.ShowDate = showDate.IsChecked == true;
        _configuration.SizeScale = sizeBox.Value;
        _onConfigurationChanged?.Invoke(_configuration);
        WidgetSurfaceStyle.PulseScale(RootBorder, _theme);
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
