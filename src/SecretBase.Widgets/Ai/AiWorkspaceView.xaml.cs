using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Ai;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Ai;
using SecretBase.Widgets.Theming;

namespace SecretBase.Widgets.Ai;

/// <summary>
/// AI Workspace hub UI. Opens built-in AI tools via host callbacks only.
/// No arbitrary exe / PowerShell / Host Bridge.
/// </summary>
public sealed partial class AiWorkspaceView : UserControl
{
    private AiCommandService? _commands;
    private Func<AiCommandResult, string?>? _tryExecute;
    private ThemeDefinition? _theme;

    public AiWorkspaceView()
    {
        InitializeComponent();
    }

    public void Initialize(
        AiCommandService commands,
        Func<AiCommandResult, string?> tryExecute)
    {
        _commands = commands;
        _tryExecute = tryExecute;
        StatusLabel.Text = string.Empty;
        Rebuild();
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        _theme = theme;
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        RootBorder.BorderBrush = ThemePainter.Brush(theme.WidgetForeground, 0.25);

        var font = new FontFamily(theme.FontFamily);
        HeaderText.FontFamily = font;
        HeaderText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        SubtitleText.FontFamily = font;
        SubtitleText.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        StatusLabel.FontFamily = font;
        StatusLabel.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        Rebuild();
    }

    private void Rebuild()
    {
        ToolsList.Children.Clear();
        if (_commands is null)
        {
            return;
        }

        var listed = _commands.Execute(AiCommand.ListTools());
        if (!listed.Succeeded)
        {
            StatusLabel.Text = listed.ErrorMessage ?? "Could not list AI tools.";
            return;
        }

        foreach (var tool in listed.Tools)
        {
            ToolsList.Children.Add(CreateToolRow(tool));
        }
    }

    private UIElement CreateToolRow(AiToolDefinition tool)
    {
        var open = new Button
        {
            Content = "Open",
            MinWidth = 72,
            Padding = new Thickness(10, 4, 10, 4),
            Tag = tool.Id
        };
        open.Click += OpenTool_Click;

        var title = new TextBlock
        {
            Text = $"{tool.Glyph}  {tool.DisplayName}",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        };
        var detail = new TextBlock
        {
            Text = tool.IsDesktopApp
                ? "Desktop app · Project root when opened from Dashboard"
                : "Official website · system browser",
            FontSize = 10,
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords
        };

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(title);
        text.Children.Add(detail);

        if (_theme is not null)
        {
            StyleActionButton(open, _theme, accent: true);
            title.FontFamily = new FontFamily(_theme.FontFamily);
            title.Foreground = ThemePainter.Brush(_theme.WidgetForeground);
            detail.FontFamily = new FontFamily(_theme.FontFamily);
            detail.Foreground = ThemePainter.Brush(_theme.ForegroundMuted);
        }

        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 0);
        Grid.SetColumn(open, 1);
        row.Children.Add(text);
        row.Children.Add(open);
        return row;
    }

    private static void StyleActionButton(Button button, ThemeDefinition theme, bool accent = false)
    {
        button.FontFamily = new FontFamily(theme.FontFamily);
        button.Background = accent
            ? ThemePainter.Brush(theme.Accent, 0.85)
            : ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        button.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        button.BorderBrush = ThemePainter.Brush(theme.Accent, 0.55);
        button.BorderThickness = new Thickness(1);
    }

    private async void OpenTool_Click(object sender, RoutedEventArgs e)
    {
        if (_commands is null || _tryExecute is null || sender is not Button { Tag: string toolId })
        {
            return;
        }

        var result = _commands.Execute(AiCommand.OpenTool(toolId));
        if (!result.Succeeded && result.OfferCursorWebsiteFallback)
        {
            var dialog = new ContentDialog
            {
                Title = "Cursor is not available",
                Content = "Cursor was not found on PATH or in LocalAppData\\Programs. Open the official website instead?",
                PrimaryButtonText = "Open Cursor Website",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var website = _commands.Execute(AiCommand.OpenCursorWebsite());
                StatusLabel.Text = _tryExecute(website) ?? "Opened Cursor website.";
            }
            else
            {
                StatusLabel.Text = result.ErrorMessage ?? "Cursor is not available.";
            }

            return;
        }

        var error = _tryExecute(result);
        StatusLabel.Text = error ?? (result.Succeeded ? $"Opened {result.Tool?.DisplayName ?? "tool"}." : (result.ErrorMessage ?? "Open failed."));
    }
}
