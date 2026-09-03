using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Base;
using SecretBase.Core.Themes;
using SecretBase.Core.Workspace;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Dashboard;

/// <summary>Personal Space status card. Quiet suggestions only — no toast spam.</summary>
public sealed partial class DashboardWidgetView : UserControl
{
    private Func<BaseDashboardSnapshot>? _snapshot;
    private Func<string, WorkspaceSession>? _prepare;
    private Func<WorkspaceSession, Task>? _continueAsync;
    private Action? _dismiss;
    private IBaseExperienceServices? _base;
    private ThemeDefinition? _theme;

    public DashboardWidgetView()
    {
        InitializeComponent();
    }

    public void Initialize(
        IBaseExperienceServices services,
        Func<BaseDashboardSnapshot> snapshot,
        Func<string, WorkspaceSession> prepare,
        Func<WorkspaceSession, Task> continueAsync,
        Action dismiss)
    {
        _base = services;
        _snapshot = snapshot;
        _prepare = prepare;
        _continueAsync = continueAsync;
        _dismiss = dismiss;
        Refresh();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyBody(TimeText, theme);
        WidgetSurfaceStyle.ApplyHeader(GreetingText, ReadyText, theme);
        WidgetSurfaceStyle.ApplyBody(ProjectText, theme);
        WidgetSurfaceStyle.ApplyMuted(SessionText, theme);
        WidgetSurfaceStyle.ApplyMuted(NextText, theme);
        WidgetSurfaceStyle.ApplyMuted(SuggestionText, theme);
        WidgetSurfaceStyle.ApplyMuted(CalendarLabel, theme);
        WidgetSurfaceStyle.ApplyBody(CalendarValue, theme);
        WidgetSurfaceStyle.ApplyMuted(TasksLabel, theme);
        WidgetSurfaceStyle.ApplyBody(TasksValue, theme);
        WidgetSurfaceStyle.ApplyMuted(MusicLabel, theme);
        WidgetSurfaceStyle.ApplyBody(MusicValue, theme);
        WidgetSurfaceStyle.ApplyMuted(AiLabel, theme);
        WidgetSurfaceStyle.ApplyBody(AiValue, theme);
        WidgetSurfaceStyle.ApplyActionButton(ContinueButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(NotNowButton, theme);
        Refresh();
    }

    public void Refresh()
    {
        if (_snapshot is null)
        {
            return;
        }

        var card = _snapshot();
        TimeText.Text = card.Time;
        GreetingText.Text = card.Greeting;
        ReadyText.Text = card.ReadyLine;
        ProjectText.Text = string.IsNullOrWhiteSpace(card.ContinuationTitle)
            ? "Nothing prepared yet"
            : card.ContinuationTitle;
        SessionText.Text = card.ContinuationDetail;
        NextText.Text = card.NextTaskLine;
        SuggestionText.Text = string.IsNullOrWhiteSpace(card.SuggestionTitle)
            ? string.Empty
            : card.SuggestionTitle + (string.IsNullOrWhiteSpace(card.SuggestionDetail)
                ? string.Empty
                : Environment.NewLine + card.SuggestionDetail);
        CalendarValue.Text = card.CalendarLine;
        TasksValue.Text = card.TasksLine;
        MusicValue.Text = card.MusicLine;
        AiValue.Text = string.IsNullOrWhiteSpace(card.AttentionLine)
            ? card.AiLine
            : card.AiLine + Environment.NewLine + card.AttentionLine;
        ContinueButton.IsEnabled = card.ShowContinue || !string.IsNullOrWhiteSpace(card.SuggestionTitle);
        NotNowButton.Visibility = string.IsNullOrWhiteSpace(card.SuggestionTitle)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_prepare is null || _continueAsync is null || _base is null)
        {
            return;
        }

        var session = _base.CurrentWorkspace ?? _prepare("Continue");
        await _continueAsync(session);
        Refresh();
        if (_theme is not null)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
    }

    private void NotNowButton_Click(object sender, RoutedEventArgs e)
    {
        _base?.RecordFeedback(false);
        _dismiss?.Invoke();
        Refresh();
    }
}
