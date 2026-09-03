using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SecretBase.Core.Activity;
using SecretBase.Core.Base;
using SecretBase.Core.Themes;
using SecretBase.Core.Workspace;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Workspace;

/// <summary>Prepared work mode. Does not launch apps until the host confirms Continue.</summary>
public sealed partial class WorkspaceWidgetView : UserControl
{
    private IBaseExperienceServices? _base;
    private Func<string, WorkspaceSession>? _prepare;
    private Func<WorkspaceSession, Task>? _continueAsync;
    private ThemeDefinition? _theme;

    public WorkspaceWidgetView()
    {
        InitializeComponent();
    }

    public void Initialize(
        IBaseExperienceServices services,
        Func<string, WorkspaceSession> prepare,
        Func<WorkspaceSession, Task> continueAsync)
    {
        _base = services;
        _prepare = prepare;
        _continueAsync = continueAsync;
        Refresh();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        WidgetSurfaceStyle.ApplyLayeredChrome(OuterShell, RootBorder, theme);
        WidgetSurfaceStyle.ApplyHeader(HeaderText, SubtitleText, theme);
        WidgetSurfaceStyle.ApplyBody(TitleText, theme);
        WidgetSurfaceStyle.ApplyMuted(LastSessionText, theme);
        WidgetSurfaceStyle.ApplyMuted(PreparedText, theme);
        WidgetSurfaceStyle.ApplyMuted(AppsText, theme);
        WidgetSurfaceStyle.ApplyMuted(FilesText, theme);
        WidgetSurfaceStyle.ApplyBody(NextText, theme);
        WidgetSurfaceStyle.ApplyMuted(FocusText, theme);
        WidgetSurfaceStyle.ApplyMuted(EmptyText, theme);
        WidgetSurfaceStyle.ApplyActionButton(PrepareButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyActionButton(ContinueButton, theme, accent: true);
        WidgetSurfaceStyle.ApplyGhostButton(FocusButton, theme);
        Refresh();
    }

    public void Refresh()
    {
        if (_base is null)
        {
            return;
        }

        var session = _base.CurrentWorkspace;
        var hasSession = session is not null;
        EmptyText.Visibility = hasSession ? Visibility.Collapsed : Visibility.Visible;
        TitleText.Text = hasSession ? session!.Title : "Nothing prepared yet";
        LastSessionText.Text = hasSession ? "Last session: " + session!.LastSessionSummary : string.Empty;
        PreparedText.Text = hasSession && session!.PreparedChecks.Count > 0
            ? "Prepared:\n" + string.Join('\n', session.PreparedChecks.Select(check => "✓ " + check))
            : string.Empty;
        AppsText.Text = hasSession && session!.SuggestedAppNames.Count > 0
            ? "Apps  " + string.Join("   ", session.SuggestedAppNames)
            : string.Empty;
        FilesText.Text = hasSession && session!.SuggestedFileNames.Count > 0
            ? "Files  " + string.Join("   ", session.SuggestedFileNames)
            : string.Empty;
        NextText.Text = hasSession && !string.IsNullOrWhiteSpace(session!.NextTask)
            ? "Next  " + session.NextTask
            : string.Empty;
        FocusText.Text = _base.Focus.Current.StatusLine(_base.Now);
        ContinueButton.IsEnabled = hasSession;
    }

    private void PrepareButton_Click(object sender, RoutedEventArgs e)
    {
        if (_prepare is null || _base is null)
        {
            return;
        }

        _base.CurrentWorkspace = _prepare("Continue");
        Refresh();
        if (_theme is not null)
        {
            WidgetSurfaceStyle.PulseScale(RootBorder);
        }
    }

    private async void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (_base?.CurrentWorkspace is null || _continueAsync is null)
        {
            return;
        }

        await _continueAsync(_base.CurrentWorkspace);
        Refresh();
    }

    private void FocusButton_Click(object sender, RoutedEventArgs e)
    {
        _base?.Focus.Start(_base.Now);
        _base?.Activity.Record(new ActivityEvent
        {
            Kind = ActivityKind.FocusStarted,
            Title = "Focus",
            ProjectName = _base.CurrentWorkspace?.ProjectName,
            At = _base.Now
        });
        Refresh();
    }
}
