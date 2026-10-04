using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Base;
using SecretBase.Core.Focus;
using SecretBase.Core.Themes;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Pomodoro;

/// <summary>Pomodoro timer surface over shared <see cref="FocusSessionStore"/>.</summary>
public sealed partial class PomodoroWidgetView : UserControl
{
    private IBaseExperienceServices? _base;
    private ThemeDefinition? _theme;
    private DispatcherTimer? _timer;

    public PomodoroWidgetView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void Initialize(IBaseExperienceServices services)
    {
        _base = services;
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
        WidgetSurfaceStyle.ApplyActionButton(PrimaryButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(ResetButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(StopButton, theme);
        Refresh();
    }

    public void Refresh()
    {
        if (_base is null)
        {
            return;
        }

        var now = _base.Now;
        var session = _base.Focus.AdvanceIfComplete(now);
        var remaining = session.Remaining(now);
        TimerText.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";

        if (!session.IsRunning)
        {
            PhaseText.Text = "Ready";
            RoundText.Text = "Round 1/4";
            PrimaryButton.Content = "Start";
            PrimaryButton.IsEnabled = true;
            ResetButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            return;
        }

        PhaseText.Text = session.IsPaused
            ? $"{session.PhaseLabel} · paused"
            : session.PhaseLabel;
        RoundText.Text = session.RoundProgressLine;
        PrimaryButton.Content = session.IsPaused ? "Resume" : "Pause";
        PrimaryButton.IsEnabled = true;
        ResetButton.IsEnabled = true;
        StopButton.IsEnabled = true;
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
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= Timer_Tick;
    }

    private void Timer_Tick(object? sender, object e) => Refresh();

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_base is null)
        {
            return;
        }

        var session = _base.Focus.Current;
        if (!session.IsRunning)
        {
            _base.Focus.Start(_base.Now);
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
        Refresh();
    }
}
