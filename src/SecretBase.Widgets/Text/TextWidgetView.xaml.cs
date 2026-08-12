using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets.Text;
using SecretBase.Widgets.Theming;
using Windows.System;

namespace SecretBase.Widgets.Text;

/// <summary>
/// Local-only text/memo widget. No network/file/process access.
/// Display ↔ edit via double-click; host chrome (move/resize) stays in WidgetFrame.
/// </summary>
public sealed partial class TextWidgetView : UserControl
{
    private TextWidgetConfiguration _configuration = TextWidgetConfiguration.CreateDefault();
    private Action<TextWidgetConfiguration>? _onConfigurationChanged;
    private bool _isEditing;
    private string _textBeforeEdit = string.Empty;

    public TextWidgetView()
    {
        InitializeComponent();
    }

    public void Initialize(
        TextWidgetConfiguration configuration,
        Action<TextWidgetConfiguration>? onConfigurationChanged = null)
    {
        _configuration = configuration;
        _onConfigurationChanged = onConfigurationChanged;
        _isEditing = false;
        ApplyConfigurationToDisplay();
        EditBox.Visibility = Visibility.Collapsed;
        DisplayText.Visibility = Visibility.Visible;
    }

    public void ApplyTheme(ThemeDefinition theme)
    {
        RootBorder.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        RootBorder.CornerRadius = new CornerRadius(theme.CornerRadius);
        DisplayText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        DisplayText.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(theme.FontFamily);
        EditBox.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        EditBox.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(theme.FontFamily);
    }

    private void ApplyConfigurationToDisplay()
    {
        DisplayText.Text = _configuration.Text;
        DisplayText.FontSize = _configuration.FontSize;
        DisplayText.TextAlignment = MapAlignment(_configuration.TextAlignment);
        EditBox.FontSize = _configuration.FontSize;
        EditBox.TextAlignment = MapAlignment(_configuration.TextAlignment);
    }

    private static Microsoft.UI.Xaml.TextAlignment MapAlignment(TextWidgetAlignment alignment) =>
        alignment switch
        {
            TextWidgetAlignment.Center => Microsoft.UI.Xaml.TextAlignment.Center,
            TextWidgetAlignment.Right => Microsoft.UI.Xaml.TextAlignment.Right,
            _ => Microsoft.UI.Xaml.TextAlignment.Left
        };

    private void DisplayText_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        EnterEditMode();
        e.Handled = true;
    }

    private void EnterEditMode()
    {
        if (_isEditing)
        {
            return;
        }

        _isEditing = true;
        _textBeforeEdit = _configuration.Text;
        EditBox.Text = _configuration.Text;
        DisplayText.Visibility = Visibility.Collapsed;
        EditBox.Visibility = Visibility.Visible;
        EditBox.Focus(FocusState.Programmatic);
        EditBox.SelectAll();
    }

    private void ExitEditMode(bool commit)
    {
        if (_isEditing)
        {
            if (commit)
            {
                _configuration.Text = EditBox.Text ?? string.Empty;
                _onConfigurationChanged?.Invoke(_configuration);
            }
            else
            {
                _configuration.Text = _textBeforeEdit;
            }

            _isEditing = false;
        }

        ApplyConfigurationToDisplay();
        EditBox.Visibility = Visibility.Collapsed;
        DisplayText.Visibility = Visibility.Visible;
    }

    private void EditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isEditing)
        {
            ExitEditMode(commit: true);
        }
    }

    private void EditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isEditing)
        {
            return;
        }

        // Escape cancels. Commit is Ctrl+Enter (accelerator) or focus loss.
        if (e.Key == VirtualKey.Escape)
        {
            ExitEditMode(commit: false);
            e.Handled = true;
        }
    }

    private void EditBox_CommitAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!_isEditing)
        {
            return;
        }

        ExitEditMode(commit: true);
        args.Handled = true;
    }
}
