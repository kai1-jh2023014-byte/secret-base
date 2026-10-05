using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Base;
using SecretBase.Core.Focus;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Pomodoro;
using SecretBase.Widgets.Theming;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;

namespace SecretBase.Widgets.Pomodoro;

/// <summary>Pomodoro timer surface over shared <see cref="FocusSessionStore"/>.</summary>
public sealed partial class PomodoroWidgetView : UserControl
{
    public const double CompactWidth = 200;
    public const double CompactHeight = 160;
    public const double ExpandedWidth = 300;
    public const double ExpandedHeight = 320;

    private IBaseExperienceServices? _base;
    private ThemeDefinition? _theme;
    private DispatcherTimer? _timer;
    private PomodoroWidgetConfiguration _configuration = PomodoroWidgetConfiguration.CreateDefault();
    private Action<PomodoroWidgetConfiguration>? _persist;
    private Action<double, double>? _onPreferredSizeChanged;
    private string? _chimeDirectory;
    private MediaPlayer? _mediaPlayer;
    private bool _suppressDurationEvents;

    public PomodoroWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(
        IBaseExperienceServices services,
        PomodoroWidgetConfiguration? configuration = null,
        Action<PomodoroWidgetConfiguration>? persist = null,
        string? chimeDirectory = null,
        Action<double, double>? onPreferredSizeChanged = null)
    {
        _base = services;
        _configuration = configuration ?? PomodoroWidgetConfiguration.CreateDefault();
        _persist = persist;
        _onPreferredSizeChanged = onPreferredSizeChanged;
        _chimeDirectory = chimeDirectory;
        ApplyConfigurationToUi();
        ApplyDurationsToStore();
        ApplyCompactLayout(persistSize: false);
        Refresh();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SubtitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(PhaseText, theme);
        WidgetSurfaceStyle.ApplyBody(TimerText, theme);
        WidgetSurfaceStyle.ApplyMuted(RoundText, theme);
        WidgetSurfaceStyle.ApplyMuted(HintText, theme);
        WidgetSurfaceStyle.ApplyMuted(CompactPhaseText, theme);
        WidgetSurfaceStyle.ApplyBody(CompactTimerText, theme);
        WidgetSurfaceStyle.ApplyActionButton(PrimaryButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyActionButton(CompactPrimaryButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ResetButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(StopButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(CompactModeButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(CompactExpandButton, theme);
        SoundCheckBox.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        ApplyCompactLayout(persistSize: false);
        Refresh();
    }

    public void Refresh()
    {
        if (_base is null)
        {
            return;
        }

        var now = _base.Now;
        var before = _base.Focus.Current;
        var completedPhase = before.IsRunning && !before.IsPaused && before.IsComplete(now);
        var session = _base.Focus.AdvanceIfComplete(now);
        if (completedPhase && _configuration.SoundOnComplete)
        {
            _ = PlayChimeAsync();
        }

        var remaining = session.Remaining(now);
        var timeText = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        TimerText.Text = timeText;
        CompactTimerText.Text = timeText;
        HintText.Text =
            $"{_configuration.FocusMinutes} min focus · {_configuration.ShortBreakMinutes} min break · "
            + $"long break {_configuration.LongBreakMinutes}m after 4 rounds";

        if (!session.IsRunning)
        {
            PhaseText.Text = "Ready";
            CompactPhaseText.Text = "Ready";
            RoundText.Text = "Round 1/4";
            PrimaryButton.Content = "Start";
            CompactPrimaryButton.Content = "Start";
            PrimaryButton.IsEnabled = true;
            CompactPrimaryButton.IsEnabled = true;
            ResetButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            FocusMinutesBox.IsEnabled = true;
            BreakMinutesBox.IsEnabled = true;
            return;
        }

        var phase = session.IsPaused
            ? $"{session.PhaseLabel} · paused"
            : session.PhaseLabel;
        PhaseText.Text = phase;
        CompactPhaseText.Text = phase;
        RoundText.Text = session.RoundProgressLine;
        var primary = session.IsPaused ? "Resume" : "Pause";
        PrimaryButton.Content = primary;
        CompactPrimaryButton.Content = primary;
        PrimaryButton.IsEnabled = true;
        CompactPrimaryButton.IsEnabled = true;
        ResetButton.IsEnabled = true;
        StopButton.IsEnabled = true;
        FocusMinutesBox.IsEnabled = false;
        BreakMinutesBox.IsEnabled = false;
    }

    private async Task PlayChimeAsync()
    {
        try
        {
            var directory = _chimeDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SecretBase",
                    "sounds");
            var path = FocusCompletionChime.EnsureWavFile(directory);
            var file = await StorageFile.GetFileFromPathAsync(path);
            _mediaPlayer ??= new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.SoundEffects };
            _mediaPlayer.Source = MediaSource.CreateFromStorageFile(file);
            _mediaPlayer.Play();
        }
        catch
        {
            // Sound is best-effort; never break the timer UI.
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick -= Timer_Tick;
        _timer.Tick += Timer_Tick;
        _timer.Start();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
        }

        _mediaPlayer?.Dispose();
        _mediaPlayer = null;
    }

    private void Timer_Tick(object? sender, object e) => Refresh();

    private void CompactModeButton_Click(object sender, RoutedEventArgs e)
    {
        _configuration.IsCompact = !_configuration.IsCompact;
        _persist?.Invoke(_configuration);
        ApplyCompactLayout(persistSize: true);
        if (_theme is not null)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
    }

    private void ApplyCompactLayout(bool persistSize)
    {
        var compact = _configuration.IsCompact;
        CompactPanel.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        FullPanel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        RootBorder.Padding = compact ? new Thickness(12, 10, 12, 10) : new Thickness(16, 14, 16, 14);
        if (!persistSize)
        {
            return;
        }

        if (compact)
        {
            _onPreferredSizeChanged?.Invoke(CompactWidth, CompactHeight);
        }
        else
        {
            _onPreferredSizeChanged?.Invoke(ExpandedWidth, ExpandedHeight);
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null)
        {
            return;
        }

        ApplyDurationsToStore();
        var session = _base.Focus.Current;
        if (!session.IsRunning)
        {
            _base.Focus.Start(
                _base.Now,
                TimeSpan.FromMinutes(_configuration.FocusMinutes),
                label: "Pomodoro",
                shortBreak: TimeSpan.FromMinutes(_configuration.ShortBreakMinutes),
                longBreak: TimeSpan.FromMinutes(_configuration.LongBreakMinutes));
        }
        else if (session.IsPaused)
        {
            _base.Focus.Resume(_base.Now);
        }
        else
        {
            _base.Focus.Pause(_base.Now);
        }

        Refresh();
        if (_theme is not null)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null)
        {
            return;
        }

        _base.Focus.Reset(_base.Now);
        Refresh();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _base?.Focus.Stop();
        ApplyDurationsToStore();
        Refresh();
    }

    private void DurationBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressDurationEvents || _base is null)
        {
            return;
        }

        if (double.IsNaN(FocusMinutesBox.Value) || double.IsNaN(BreakMinutesBox.Value))
        {
            return;
        }

        _configuration.FocusMinutes = (int)Math.Clamp(FocusMinutesBox.Value, 1, 120);
        _configuration.ShortBreakMinutes = (int)Math.Clamp(BreakMinutesBox.Value, 1, 60);
        ApplyDurationsToStore();
        _persist?.Invoke(_configuration);
        Refresh();
    }

    private void SoundCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressDurationEvents)
        {
            return;
        }

        _configuration.SoundOnComplete = SoundCheckBox.IsChecked == true;
        _persist?.Invoke(_configuration);
    }

    private void ApplyConfigurationToUi()
    {
        _suppressDurationEvents = true;
        FocusMinutesBox.Value = _configuration.FocusMinutes;
        BreakMinutesBox.Value = _configuration.ShortBreakMinutes;
        SoundCheckBox.IsChecked = _configuration.SoundOnComplete;
        _suppressDurationEvents = false;
    }

    private void ApplyDurationsToStore()
    {
        _base?.Focus.ConfigureDurations(
            TimeSpan.FromMinutes(_configuration.FocusMinutes),
            TimeSpan.FromMinutes(_configuration.ShortBreakMinutes),
            TimeSpan.FromMinutes(_configuration.LongBreakMinutes));
    }
}
