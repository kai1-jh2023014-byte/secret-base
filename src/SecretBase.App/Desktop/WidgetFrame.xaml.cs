using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Desktop;
using SecretBase.Core.Themes;
using SecretBase.Core.Widgets;
using SecretBase.Widgets.Hosting;
using SecretBase.Widgets.Theming;
using Windows.Foundation;

namespace SecretBase.App.Desktop;

/// <summary>
/// Host chrome for a widget instance: drag move + corner resize + remove.
/// Kept visually light so widgets feel like floating desktop objects.
/// </summary>
public sealed partial class WidgetFrame : UserControl
{
    private readonly WidgetInstance _instance;
    private readonly ThemeDefinition _theme;
    private readonly Action _onLayoutCommitted;
    private readonly Action? _onBoundsChanged;
    private readonly Action<WidgetInstance>? _onRemoveRequested;
    private readonly Action<WidgetFrame>? _onSelectionRequested;

    private bool _dragging;
    private bool _resizing;
    private bool _pointerInside;
    private bool _selected;
    private bool _quietChrome;
    private Point _lastPoint;

    public WidgetFrame(
        WidgetInstance instance,
        UIElement content,
        ThemeDefinition theme,
        Action onLayoutCommitted,
        Action? onBoundsChanged = null,
        Action<WidgetInstance>? onRemoveRequested = null,
        Action<WidgetFrame>? onSelectionRequested = null)
    {
        InitializeComponent();
        _instance = instance;
        _theme = theme;
        _onLayoutCommitted = onLayoutCommitted;
        _onBoundsChanged = onBoundsChanged;
        _onRemoveRequested = onRemoveRequested;
        _onSelectionRequested = onSelectionRequested;

        ContentHost.Child = content;
        Width = instance.Size.Width;
        Height = instance.Size.Height;
        ApplyFloatingChrome(theme);
        SetChromeEmphasis(emphasized: false);
        RootGrid.PointerPressed += RootGrid_PointerPressed;
    }

    public Guid WidgetId => _instance.Id;

    public string WidgetType => _instance.Type;

    /// <summary>
    /// When true, × / drag / resize stay fully hidden until the widget is selected or hovered.
    /// Used by Progress minimal (transparent) mode.
    /// </summary>
    public bool QuietChrome
    {
        get => _quietChrome;
        set
        {
            if (_quietChrome == value)
            {
                return;
            }

            _quietChrome = value;
            SetChromeEmphasis(emphasized: _selected || _pointerInside || _dragging || _resizing);
        }
    }

    public bool IsSelected => _selected;

    public void SetSelected(bool selected)
    {
        if (_selected == selected)
        {
            return;
        }

        _selected = selected;
        SetChromeEmphasis(emphasized: _selected || _pointerInside || _dragging || _resizing);
        if (ContentHost.Child is IWidgetChromeAware chromeAware)
        {
            chromeAware.SetChromeVisible(_selected || _pointerInside);
        }
    }

    /// <summary>Apply a preferred size from a widget compact/expand toggle and persist via layout commit.</summary>
    public void ApplyPreferredSize(double width, double height, bool commit = true)
    {
        _instance.Size.Width = width;
        _instance.Size.Height = height;
        _instance.Size.Clamp(_theme.WidgetMinWidth, _theme.WidgetMinHeight);
        Width = _instance.Size.Width;
        Height = _instance.Size.Height;
        _onBoundsChanged?.Invoke();
        if (commit)
        {
            _onLayoutCommitted();
        }
    }

    private void ApplyFloatingChrome(ThemeDefinition theme)
    {
        var radius = Math.Max(10, theme.CornerRadius);
        DragBar.CornerRadius = new CornerRadius(radius, 0, 0, 0);
        RemoveButton.CornerRadius = new CornerRadius(0, radius, 0, 0);
        RemoveButton.FontFamily = new FontFamily(theme.FontFamily);
        RemoveButton.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        RemoveButton.Background = ThemePainter.Brush(theme.SurfaceSecondary, 0.55);
        RemoveButton.BorderThickness = new Thickness(0);

        var grip = ThemePainter.ParseColor(theme.WidgetForeground);
        grip.A = 0x18;
        DragBar.Background = new SolidColorBrush(grip);

        var dot = ThemePainter.Brush(theme.WidgetForeground, 0.45);
        GripDot1.Fill = dot;
        GripDot2.Fill = dot;
        GripDot3.Fill = dot;
        ResizeHandle.Background = ThemePainter.Brush(theme.Border, 0.55);
        ResizeHandle.CornerRadius = new CornerRadius(Math.Max(3, theme.CornerRadius * 0.2));
    }

    private void SetChromeEmphasis(bool emphasized)
    {
        var active = emphasized || _dragging || _resizing || _selected;
        double opacity;
        if (_quietChrome)
        {
            opacity = active ? 0.96 : 0;
            var visibility = active ? Visibility.Visible : Visibility.Collapsed;
            ChromeRow.Visibility = visibility;
            DragBar.Visibility = visibility;
            RemoveButton.Visibility = visibility;
            ResizeHandle.Visibility = visibility;
            // Collapse the 22px chrome row so minimal Progress sits flush.
            RootGrid.RowDefinitions[0].Height = active ? new GridLength(22) : new GridLength(0);
        }
        else
        {
            opacity = active ? 0.96 : 0.28;
            ChromeRow.Visibility = Visibility.Visible;
            DragBar.Visibility = Visibility.Visible;
            RemoveButton.Visibility = Visibility.Visible;
            ResizeHandle.Visibility = Visibility.Visible;
            RootGrid.RowDefinitions[0].Height = new GridLength(22);
        }

        WidgetSurfaceStyle.FadeOpacity(DragBar, opacity, active ? 140 : 220);
        WidgetSurfaceStyle.FadeOpacity(ResizeHandle, opacity, active ? 140 : 220);
        WidgetSurfaceStyle.FadeOpacity(RemoveButton, opacity, active ? 140 : 220);

        if (ContentHost.Child is IWidgetChromeAware chromeAware)
        {
            chromeAware.SetChromeVisible(active || _pointerInside);
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e) =>
        _onRemoveRequested?.Invoke(_instance);

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind is PointerUpdateKind.LeftButtonPressed)
        {
            _onSelectionRequested?.Invoke(this);
        }
    }

    private void RootGrid_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerInside = true;
        SetChromeEmphasis(emphasized: true);
    }

    private void RootGrid_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerInside = false;
        if (!_dragging && !_resizing)
        {
            SetChromeEmphasis(emphasized: _selected);
        }
    }

    private void DragArea_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _dragging = true;
        SetChromeEmphasis(emphasized: true);
        _lastPoint = e.GetCurrentPoint((UIElement)Parent).Position;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void DragArea_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || Parent is not Canvas)
        {
            return;
        }

        var point = e.GetCurrentPoint((UIElement)Parent).Position;
        var dx = point.X - _lastPoint.X;
        var dy = point.Y - _lastPoint.Y;
        _lastPoint = point;

        var newX = Canvas.GetLeft(this) + dx;
        var newY = Canvas.GetTop(this) + dy;
        ClampToParentCanvas(ref newX, ref newY, Width, Height);
        Canvas.SetLeft(this, newX);
        Canvas.SetTop(this, newY);
        _instance.Position.X = newX;
        _instance.Position.Y = newY;
        _onBoundsChanged?.Invoke();
        e.Handled = true;
    }

    private void DragArea_PointerReleased(object sender, PointerRoutedEventArgs e) => EndDrag(sender, e);

    private void DragArea_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndDrag(sender, e, captureLost: true);

    private void EndDrag(object sender, PointerRoutedEventArgs e, bool captureLost = false)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (!captureLost)
        {
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        }

        SetChromeEmphasis(emphasized: _pointerInside || _selected);
        _onBoundsChanged?.Invoke();
        _onLayoutCommitted();
        e.Handled = true;
    }

    private void ResizeHandle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _resizing = true;
        SetChromeEmphasis(emphasized: true);
        _lastPoint = e.GetCurrentPoint((UIElement)Parent).Position;
        ((UIElement)sender).CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ResizeHandle_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_resizing)
        {
            return;
        }

        var point = e.GetCurrentPoint((UIElement)Parent).Position;
        var dx = point.X - _lastPoint.X;
        var dy = point.Y - _lastPoint.Y;
        _lastPoint = point;

        _instance.Size.Width += dx;
        _instance.Size.Height += dy;
        var maxW = Parent is Canvas canvas && canvas.ActualWidth > 0
            ? Math.Max(_theme.WidgetMinWidth, canvas.ActualWidth - Canvas.GetLeft(this))
            : (double?)null;
        var maxH = Parent is Canvas canvasH && canvasH.ActualHeight > 0
            ? Math.Max(
                _theme.WidgetMinHeight,
                canvasH.ActualHeight - Canvas.GetTop(this) - DesktopViewportLayout.DefaultBottomReserve)
            : (double?)null;
        _instance.Size.Clamp(_theme.WidgetMinWidth, _theme.WidgetMinHeight, maxW, maxH);
        Width = _instance.Size.Width;
        Height = _instance.Size.Height;
        _onBoundsChanged?.Invoke();
        e.Handled = true;
    }

    private void ResizeHandle_PointerReleased(object sender, PointerRoutedEventArgs e) => EndResize(sender, e);

    private void ResizeHandle_PointerCaptureLost(object sender, PointerRoutedEventArgs e) => EndResize(sender, e, captureLost: true);

    private void EndResize(object sender, PointerRoutedEventArgs e, bool captureLost = false)
    {
        if (!_resizing)
        {
            return;
        }

        _resizing = false;
        if (!captureLost)
        {
            ((UIElement)sender).ReleasePointerCapture(e.Pointer);
        }

        SetChromeEmphasis(emphasized: _pointerInside || _selected);
        _onBoundsChanged?.Invoke();
        _onLayoutCommitted();
        e.Handled = true;
    }

    private void ClampToParentCanvas(ref double x, ref double y, double width, double height)
    {
        if (Parent is not Canvas canvas)
        {
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            return;
        }

        var maxX = canvas.ActualWidth > 0
            ? Math.Max(0, canvas.ActualWidth - Math.Max(40, width))
            : double.MaxValue;
        var maxY = canvas.ActualHeight > 0
            ? Math.Max(0, canvas.ActualHeight - Math.Max(40, height) - DesktopViewportLayout.DefaultBottomReserve)
            : double.MaxValue;
        x = Math.Clamp(x, 0, maxX);
        y = Math.Clamp(y, 0, maxY);
    }
}
