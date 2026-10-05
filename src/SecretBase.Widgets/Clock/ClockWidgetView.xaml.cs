using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Themes;
using SecretBase.Core.Time;
using SecretBase.Core.Widgets.Clock;
using SecretBase.Widgets.Hosting;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Clock;

/// <summary>Atelier Clock — Base / Large digital / Minimal / Analog / Focus.</summary>
public sealed partial class ClockWidgetView : UserControl, IDisposable
{
    private readonly DispatcherTimer _timer;
    private ITimeProvider _timeProvider = new SystemTimeProvider();
    private ClockWidgetConfiguration _configuration = ClockWidgetConfiguration.CreateDefault();
    private Action<ClockWidgetConfiguration>? _onConfigurationChanged;
    private Func<ClockBaseStatus?>? _statusSource;
    private OverlayDialogInput? _dialogInput;
    private ThemeDefinition? _theme;
    private bool _disposed;
    private bool _ticksBuilt;
    private bool _pointerInside;

    public ClockWidgetView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PointerEntered += (_, _) =>
        {
            _pointerInside = true;
            ApplyChromeForStyle();
        };
        PointerExited += (_, _) =>
        {
            _pointerInside = false;
            ApplyChromeForStyle();
        };
    }

    public void Initialize(
        ClockWidgetConfiguration configuration,
        ITimeProvider? timeProvider = null,
        Action<ClockWidgetConfiguration>? onConfigurationChanged = null,
        Func<ClockBaseStatus?>? statusSource = null,
        OverlayDialogInput? dialogInput = null)
    {
        _configuration = configuration;
        _configuration.DisplayStyle = ClockWidgetConfiguration.NormalizeStyle(_configuration.DisplayStyle);
        _timeProvider = timeProvider ?? new SystemTimeProvider();
        _onConfigurationChanged = onConfigurationChanged;
        _statusSource = statusSource;
        _dialogInput = dialogInput;
        RefreshDisplay();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        ApplyChromeForStyle();
        TimeText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        DateText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusText.Foreground = ThemePainter.Brush(theme.Accent);
        NextText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        AnalogCaption.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        TimeText.FontFamily = new FontFamily(theme.FontFamily);
        DateText.FontFamily = new FontFamily(theme.FontFamily);
        StatusText.FontFamily = new FontFamily(theme.FontFamily);
        NextText.FontFamily = new FontFamily(theme.FontFamily);
        AnalogCaption.FontFamily = new FontFamily(theme.FontFamily);
        AccentHairline.Background = ThemePainter.Brush(theme.Accent, 0.9);
        AnalogRingOuter.Stroke = ThemePainter.Brush(theme.Border, 0.35);
        AnalogFace.Stroke = ThemePainter.Brush(theme.Border, 0.7);
        HourHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        MinuteHand.Stroke = ThemePainter.Brush(theme.WidgetForeground);
        SecondHand.Stroke = ThemePainter.Brush(theme.Accent, 0.95);
        AnalogCenter.Fill = ThemePainter.Brush(theme.Accent);
        AnalogMark12.Foreground = ThemePainter.Brush(theme.WidgetForeground, 0.8);
        AnalogMark3.Foreground = ThemePainter.Brush(theme.WidgetForeground, 0.8);
        AnalogMark6.Foreground = ThemePainter.Brush(theme.WidgetForeground, 0.8);
        AnalogMark9.Foreground = ThemePainter.Brush(theme.WidgetForeground, 0.8);
        WidgetSurfaceStyle.ApplyGhostButton(SettingsButton, theme);
        EnsureAnalogTicks();
        RefreshDisplay();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        EnsureAnalogTicks();
        RefreshDisplay();
        WidgetSurfaceStyle.FadeOpacity(this, 1, _theme?.MotionDurationMs ?? 220);
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
        var style = ClockWidgetConfiguration.NormalizeStyle(_configuration.DisplayStyle);
        _configuration.DisplayStyle = style;
        var isAnalog = string.Equals(style, ClockWidgetConfiguration.StyleAnalog, StringComparison.Ordinal);
        var isBase = string.Equals(style, ClockWidgetConfiguration.StyleBase, StringComparison.Ordinal);
        var isLarge = ClockWidgetConfiguration.IsLargeTypography(style);
        DigitalPanel.Visibility = isAnalog ? Visibility.Collapsed : Visibility.Visible;
        AnalogPanel.Visibility = isAnalog ? Visibility.Visible : Visibility.Collapsed;
        AccentHairline.Visibility = style is ClockWidgetConfiguration.StyleMinimal or ClockWidgetConfiguration.StyleLarge
            ? Visibility.Collapsed
            : Visibility.Visible;
        ApplyChromeForStyle();

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
            ClockWidgetConfiguration.StyleLarge => 64 * scale,
            ClockWidgetConfiguration.StyleDigital => 64 * scale,
            ClockWidgetConfiguration.StyleMinimal => 56 * scale,
            ClockWidgetConfiguration.StyleFocus => 38 * scale,
            ClockWidgetConfiguration.StyleBase => 42 * scale,
            _ => 44 * scale
        };
        TimeText.FontWeight = isLarge || style == ClockWidgetConfiguration.StyleMinimal
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Medium;
        TimeText.CharacterSpacing = isLarge ? 40 : style == ClockWidgetConfiguration.StyleMinimal ? 120 : 80;
        TimeText.HorizontalAlignment = style is ClockWidgetConfiguration.StyleMinimal
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Left;
        DateText.HorizontalAlignment = TimeText.HorizontalAlignment;
        StatusText.HorizontalAlignment = TimeText.HorizontalAlignment;
        NextText.HorizontalAlignment = TimeText.HorizontalAlignment;
        DateText.FontSize = style switch
        {
            ClockWidgetConfiguration.StyleLarge => 15 * scale,
            ClockWidgetConfiguration.StyleDigital => 15 * scale,
            ClockWidgetConfiguration.StyleMinimal => 12 * scale,
            ClockWidgetConfiguration.StyleFocus => 14 * scale,
            _ => 13 * scale
        };
        DateText.Opacity = style == ClockWidgetConfiguration.StyleMinimal ? 0.7 : 0.92;
        DateText.CharacterSpacing = isLarge ? 20 : 30;
    }

    private static bool IsFloatingWhiteStyle(string style) =>
        ClockWidgetConfiguration.IsLargeTypography(style)
        || style == ClockWidgetConfiguration.StyleMinimal;

    private void ApplyChromeForStyle()
    {
        if (_theme is null)
        {
            return;
        }

        var style = ClockWidgetConfiguration.NormalizeStyle(_configuration.DisplayStyle);
        if (IsFloatingWhiteStyle(style))
        {
            // Large / Minimal: clear by default; thin glass only while hovered.
            OuterShell.BorderThickness = new Thickness(0);
            OuterShell.Padding = new Thickness(0);
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.Padding = new Thickness(10, 8, 10, 8);
            RootBorder.CornerRadius = new CornerRadius(Math.Max(10, _theme.CornerRadius - 4));
            if (_pointerInside)
            {
                OuterShell.Background = ThemePainter.Brush(_theme.SurfaceElevated, 0.08);
                OuterShell.BorderBrush = ThemePainter.Brush(_theme.Border, 0.12);
                RootBorder.Background = ThemePainter.Brush(_theme.WidgetBackground, 0.12);
                RootBorder.BorderBrush = ThemePainter.Brush(_theme.Border, 0.12);
                SettingsButton.Opacity = 0.55;
            }
            else
            {
                OuterShell.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                OuterShell.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                RootBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                RootBorder.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                SettingsButton.Opacity = 0.0;
            }

            return;
        }

        if (style == ClockWidgetConfiguration.StyleAnalog)
        {
            WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, _theme);
            RootBorder.Padding = new Thickness(14, 12, 14, 10);
            SettingsButton.Opacity = 0.45;
            return;
        }

        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, _theme);
        SettingsButton.Opacity = 0.55;
    }

    private void EnsureAnalogTicks()
    {
        if (_ticksBuilt || AnalogTicks is null)
        {
            return;
        }

        AnalogTicks.Children.Clear();
        const double cx = 66;
        const double cy = 66;
        for (var i = 0; i < 12; i++)
        {
            var angle = i * 30.0 * Math.PI / 180.0;
            var outer = 54.0;
            var inner = i % 3 == 0 ? 46.0 : 50.0;
            var line = new Line
            {
                X1 = cx + Math.Sin(angle) * inner,
                Y1 = cy - Math.Cos(angle) * inner,
                X2 = cx + Math.Sin(angle) * outer,
                Y2 = cy - Math.Cos(angle) * outer,
                StrokeThickness = i % 3 == 0 ? 1.6 : 1.0,
                Opacity = i % 3 == 0 ? 0.85 : 0.45,
                Stroke = _theme is null
                    ? new SolidColorBrush(Microsoft.UI.Colors.White)
                    : ThemePainter.Brush(_theme.WidgetForeground, i % 3 == 0 ? 0.85 : 0.45),
                StrokeStartLineCap = Microsoft.UI.Xaml.Media.PenLineCap.Round,
                StrokeEndLineCap = Microsoft.UI.Xaml.Media.PenLineCap.Round
            };
            AnalogTicks.Children.Add(line);
        }

        _ticksBuilt = true;
    }

    private void UpdateAnalogHands(DateTimeOffset now)
    {
        EnsureAnalogTicks();
        var hour = now.Hour % 12 + now.Minute / 60.0;
        var minute = now.Minute + now.Second / 60.0;
        var second = now.Second + now.Millisecond / 1000.0;
        SetHand(HourHand, hour * 30, 28);
        SetHand(MinuteHand, minute * 6, 40);
        SetHand(SecondHand, second * 6, 44);
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
            ItemsSource = new[]
            {
                "Base status",
                "Large digital",
                "Minimal",
                "Analog",
                "Focus"
            },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        styleBox.SelectedItem = ClockWidgetConfiguration.NormalizeStyle(_configuration.DisplayStyle) switch
        {
            ClockWidgetConfiguration.StyleMinimal => "Minimal",
            ClockWidgetConfiguration.StyleAnalog => "Analog",
            ClockWidgetConfiguration.StyleFocus => "Focus",
            ClockWidgetConfiguration.StyleBase => "Base status",
            _ => "Large digital"
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

        using var _ = _dialogInput?.Enter();
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _configuration.DisplayStyle = (styleBox.SelectedItem as string) switch
        {
            "Minimal" => ClockWidgetConfiguration.StyleMinimal,
            "Analog" => ClockWidgetConfiguration.StyleAnalog,
            "Focus" => ClockWidgetConfiguration.StyleFocus,
            "Base status" => ClockWidgetConfiguration.StyleBase,
            _ => ClockWidgetConfiguration.StyleLarge
        };
        _configuration.Use24HourFormat = format24.IsChecked == true;
        _configuration.ShowSeconds = showSeconds.IsChecked == true;
        _configuration.ShowDate = showDate.IsChecked == true;
        _configuration.SizeScale = sizeBox.Value;
        _onConfigurationChanged?.Invoke(_configuration);
        WidgetSurfaceStyle.PulseScale(RootBorder);
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
