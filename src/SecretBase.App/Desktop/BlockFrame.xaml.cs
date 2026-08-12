using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SecretBase.Core.Blocks;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Theming;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace SecretBase.App.Desktop;

/// <summary>
/// Desktop Block chrome: move / resize / name / delete / movable item icons + drop targets.
/// </summary>
public sealed partial class BlockFrame : UserControl
{
    private readonly Block _block;
    private readonly ThemeDefinition _theme;
    private readonly ITargetLaunchService _launcher;
    private readonly IFileIconService _icons;
    private readonly Action _onLayoutCommitted;
    private readonly Action? _onBoundsChanged;
    private readonly Action<Block> _onDeleteRequested;
    private readonly Action<string>? _onStatus;

    private bool _dragging;
    private bool _resizing;
    private bool _pointerInside;
    private Point _lastPoint;

    private BlockItem? _activeItem;
    private FrameworkElement? _activeTile;
    private bool _itemDragging;
    private Point _itemLastPoint;
    private Point _itemPressPoint;

    public BlockFrame(
        Block block,
        ThemeDefinition theme,
        ITargetLaunchService launcher,
        IFileIconService icons,
        Action onLayoutCommitted,
        Action<Block> onDeleteRequested,
        Action? onBoundsChanged = null,
        Action<string>? onStatus = null)
    {
        InitializeComponent();
        _block = block;
        _theme = theme;
        _launcher = launcher;
        _icons = icons;
        _onLayoutCommitted = onLayoutCommitted;
        _onDeleteRequested = onDeleteRequested;
        _onBoundsChanged = onBoundsChanged;
        _onStatus = onStatus;

        Width = block.Size.Width;
        Height = block.Size.Height;
        ApplyTheme(theme);
        RefreshItems();
        SetChromeEmphasis(emphasized: false);
        SizeChanged += (_, _) => ClampAllItemPlacements();
    }

    public Guid BlockId => _block.Id;

    public Block Block => _block;

    private void ApplyTheme(ThemeDefinition theme)
    {
        var radius = Math.Max(10, theme.CornerRadius);
        DragBar.CornerRadius = new CornerRadius(radius, radius, 0, 0);
        Surface.CornerRadius = new CornerRadius(0, 0, radius, radius);
        Surface.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        NameText.Text = _block.Name;
        NameText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        NameText.FontFamily = new FontFamily(theme.FontFamily);
        EmptyHint.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        EmptyHint.FontFamily = new FontFamily(theme.FontFamily);
        DeleteButton.Foreground = ThemePainter.Brush(theme.ForegroundMuted);

        var grip = ThemePainter.ParseColor(theme.WidgetForeground);
        grip.A = 0x28;
        DragBar.Background = new SolidColorBrush(grip);
    }

    private void RefreshItems()
    {
        ItemCanvas.Children.Clear();
        for (var i = 0; i < _block.Items.Count; i++)
        {
            var item = _block.Items[i];
            item.EnsurePlacement(i);
            item.ClampPlacement(ItemCanvas.ActualWidth > 0 ? ItemCanvas.ActualWidth : _block.Size.Width - 24,
                ItemCanvas.ActualHeight > 0 ? ItemCanvas.ActualHeight : _block.Size.Height - 60);
            ItemCanvas.Children.Add(CreateItemTile(item));
        }

        EmptyHint.Visibility = _block.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement CreateItemTile(BlockItem item)
    {
        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Name) ? "Item" : item.Name,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            Foreground = ThemePainter.Brush(_theme.WidgetForeground),
            FontFamily = new FontFamily(_theme.FontFamily)
        };

        var iconHost = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))
        };

        var iconPath = !string.IsNullOrWhiteSpace(item.Icon) && File.Exists(item.Icon)
            ? item.Icon
            : _icons.TryGetCachedIconPath(item.Target);

        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            item.Icon = iconPath;
            iconHost.Child = new Image
            {
                Source = new BitmapImage(new Uri(iconPath)),
                Stretch = Stretch.Uniform,
                Width = 36,
                Height = 36
            };
        }
        else
        {
            iconHost.Child = new TextBlock
            {
                Text = item.Type switch
                {
                    BlockItemType.Application => "APP",
                    BlockItemType.Shortcut => "LNK",
                    BlockItemType.Folder => "DIR",
                    _ => "FILE"
                },
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ThemePainter.Brush(_theme.ForegroundMuted)
            };
        }

        var stack = new StackPanel
        {
            Width = BlockItem.TileWidth,
            Spacing = 4,
            Padding = new Thickness(2)
        };
        stack.Children.Add(iconHost);
        stack.Children.Add(label);

        var tile = new Border
        {
            Width = BlockItem.TileWidth,
            Height = BlockItem.TileHeight,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(8),
            Child = stack,
            Tag = item,
            CanDrag = false
        };

        tile.PointerPressed += ItemTile_PointerPressed;
        tile.PointerMoved += ItemTile_PointerMoved;
        tile.PointerReleased += ItemTile_PointerReleased;
        tile.PointerCaptureLost += ItemTile_PointerCaptureLost;
        ToolTipService.SetToolTip(tile, $"{item.Name}\n{item.Target}\nDrag to move · Click to open");

        Canvas.SetLeft(tile, item.X);
        Canvas.SetTop(tile, item.Y);
        return tile;
    }

    private void ClampAllItemPlacements()
    {
        var maxW = ItemCanvas.ActualWidth;
        var maxH = ItemCanvas.ActualHeight;
        if (maxW <= 0 || maxH <= 0)
        {
            return;
        }

        foreach (var child in ItemCanvas.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not BlockItem item)
            {
                continue;
            }

            item.ClampPlacement(maxW, maxH);
            Canvas.SetLeft(child, item.X);
            Canvas.SetTop(child, item.Y);
        }
    }

    private void ItemTile_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not FrameworkElement tile || tile.Tag is not BlockItem item)
        {
            return;
        }

        if (e.GetCurrentPoint(tile).Properties.PointerUpdateKind is not PointerUpdateKind.LeftButtonPressed)
        {
            return;
        }

        _activeTile = tile;
        _activeItem = item;
        _itemDragging = false;
        _itemPressPoint = e.GetCurrentPoint(ItemCanvas).Position;
        _itemLastPoint = _itemPressPoint;
        tile.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void ItemTile_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_activeTile is null || _activeItem is null || sender is not FrameworkElement tile)
        {
            return;
        }

        var point = e.GetCurrentPoint(ItemCanvas).Position;
        if (!_itemDragging)
        {
            var dx = point.X - _itemPressPoint.X;
            var dy = point.Y - _itemPressPoint.Y;
            if ((dx * dx) + (dy * dy) < 25)
            {
                return;
            }

            _itemDragging = true;
        }

        var moveX = point.X - _itemLastPoint.X;
        var moveY = point.Y - _itemLastPoint.Y;
        _itemLastPoint = point;

        _activeItem.X = Canvas.GetLeft(tile) + moveX;
        _activeItem.Y = Canvas.GetTop(tile) + moveY;
        _activeItem.ClampPlacement(ItemCanvas.ActualWidth, ItemCanvas.ActualHeight);
        Canvas.SetLeft(tile, _activeItem.X);
        Canvas.SetTop(tile, _activeItem.Y);
        e.Handled = true;
    }

    private void ItemTile_PointerReleased(object sender, PointerRoutedEventArgs e) =>
        EndItemPointer(sender, e, captureLost: false);

    private void ItemTile_PointerCaptureLost(object sender, PointerRoutedEventArgs e) =>
        EndItemPointer(sender, e, captureLost: true);

    private void EndItemPointer(object sender, PointerRoutedEventArgs e, bool captureLost)
    {
        if (_activeTile is null || _activeItem is null)
        {
            return;
        }

        var item = _activeItem;
        var dragged = _itemDragging;
        if (!captureLost && sender is UIElement el)
        {
            el.ReleasePointerCapture(e.Pointer);
        }

        _activeTile = null;
        _activeItem = null;
        _itemDragging = false;

        if (dragged)
        {
            _onLayoutCommitted();
            _onBoundsChanged?.Invoke();
        }
        else
        {
            var result = _launcher.TryLaunch(new TargetLaunchRequest(
                Target: item.Target,
                ItemType: item.Type.ToString(),
                DisplayName: item.Name));
            if (!result.Succeeded)
            {
                _onStatus?.Invoke(result.ErrorMessage ?? "Launch failed.");
            }
        }

        e.Handled = true;
    }

    private void SetChromeEmphasis(bool emphasized)
    {
        var opacity = emphasized || _dragging || _resizing ? 0.95 : 0.35;
        DragBar.Opacity = opacity;
        ResizeHandle.Opacity = opacity;
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
            SetChromeEmphasis(emphasized: false);
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

        var newX = Math.Max(0, Canvas.GetLeft(this) + dx);
        var newY = Math.Max(0, Canvas.GetTop(this) + dy);
        Canvas.SetLeft(this, newX);
        Canvas.SetTop(this, newY);
        _block.Position.X = newX;
        _block.Position.Y = newY;
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

        SetChromeEmphasis(emphasized: _pointerInside);
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

        _block.Size.Width += dx;
        _block.Size.Height += dy;
        _block.ClampSize();
        Width = _block.Size.Width;
        Height = _block.Size.Height;
        ClampAllItemPlacements();
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

        SetChromeEmphasis(emphasized: _pointerInside);
        _onBoundsChanged?.Invoke();
        _onLayoutCommitted();
        e.Handled = true;
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        _onDeleteRequested(_block);

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to Block";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        IReadOnlyList<IStorageItem> items;
        try
        {
            items = await e.DataView.GetStorageItemsAsync();
        }
        catch (Exception ex)
        {
            _onStatus?.Invoke(ex.Message);
            return;
        }

        var dropPoint = e.GetPosition(ItemCanvas);
        var added = 0;
        foreach (var storageItem in items)
        {
            var path = storageItem.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var isDirectory = storageItem is StorageFolder;
            var type = BlockTargetValidator.InferType(path, isDirectory);
            if (!BlockTargetValidator.TryValidate(path, type, out var normalized, out var error))
            {
                _onStatus?.Invoke(error);
                continue;
            }

            if (_block.Items.Any(i => string.Equals(i.Target, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var iconPath = _icons.TryGetCachedIconPath(normalized) ?? string.Empty;
            var item = new BlockItem
            {
                Id = Guid.NewGuid(),
                Name = BlockTargetValidator.InferDisplayName(normalized),
                Type = type,
                Target = normalized,
                Icon = iconPath,
                X = Math.Max(0, dropPoint.X - (BlockItem.TileWidth / 2) + (added * 12)),
                Y = Math.Max(0, dropPoint.Y - (BlockItem.TileHeight / 2) + (added * 12))
            };
            item.ClampPlacement(
                ItemCanvas.ActualWidth > 0 ? ItemCanvas.ActualWidth : _block.Size.Width - 24,
                ItemCanvas.ActualHeight > 0 ? ItemCanvas.ActualHeight : _block.Size.Height - 60);

            _block.Items.Add(item);
            added++;
        }

        if (added > 0)
        {
            RefreshItems();
            _onLayoutCommitted();
            _onBoundsChanged?.Invoke();
            _onStatus?.Invoke($"Added {added} item(s) to '{_block.Name}'. Drag icons to arrange.");
        }
    }
}
