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
    private readonly IBlockItemIntakeService _intake;
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
        IBlockItemIntakeService intake,
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
        _intake = intake;
        _onLayoutCommitted = onLayoutCommitted;
        _onDeleteRequested = onDeleteRequested;
        _onBoundsChanged = onBoundsChanged;
        _onStatus = onStatus;

        Width = block.Size.Width;
        Height = block.Size.Height;
        ApplyTheme(theme);
        RefreshItems(arrangeIfNeeded: true);
        SetChromeEmphasis(emphasized: false);
        SizeChanged += (_, _) =>
        {
            ArrangeItemsEvenly();
            SyncTilePositionsFromModel();
            _onBoundsChanged?.Invoke();
        };
    }

    public Guid BlockId => _block.Id;

    public Block Block => _block;

    private void ApplyTheme(ThemeDefinition theme)
    {
        var radius = Math.Max(12, theme.CornerRadius);
        DragBar.CornerRadius = new CornerRadius(radius, radius, 0, 0);
        Surface.CornerRadius = new CornerRadius(0, 0, radius, radius);
        Surface.Background = ThemePainter.Brush(theme.WidgetBackground, ThemePainter.EffectiveWidgetOpacity(theme));
        Surface.BorderBrush = ThemePainter.Brush(theme.Border, 0.4);
        Surface.BorderThickness = new Thickness(1, 0, 1, 1);
        NameText.Text = _block.Name;
        NameText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        NameText.FontFamily = new FontFamily(theme.FontFamily);
        NameText.CharacterSpacing = 30;
        EmptyHint.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        EmptyHint.FontFamily = new FontFamily(theme.FontFamily);
        WidgetSurfaceStyle.ApplyGhostButton(DeleteButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(ArrangeButton, theme);
        ResizeHandle.Background = ThemePainter.Brush(theme.Border, 0.55);

        var grip = ThemePainter.ParseColor(theme.WidgetForeground);
        grip.A = 0x18;
        DragBar.Background = new SolidColorBrush(grip);
    }

    private double ContentWidth =>
        ItemCanvas.ActualWidth > 0 ? ItemCanvas.ActualWidth : Math.Max(BlockItem.TileWidth, _block.Size.Width - 24);

    private double ContentHeight =>
        ItemCanvas.ActualHeight > 0 ? ItemCanvas.ActualHeight : Math.Max(BlockItem.TileHeight, _block.Size.Height - 60);

    private void ArrangeItemsEvenly()
    {
        BlockItemLayout.ArrangeEvenly(_block.Items, ContentWidth, ContentHeight);
    }

    private void RefreshItems(bool arrangeIfNeeded)
    {
        if (arrangeIfNeeded || _block.Items.Any(i => !i.HasPlacement))
        {
            ArrangeItemsEvenly();
        }

        ItemCanvas.Children.Clear();
        foreach (var item in _block.Items)
        {
            item.ClampPlacement(ContentWidth, ContentHeight);
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
        foreach (var child in ItemCanvas.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not BlockItem item)
            {
                continue;
            }

            item.ClampPlacement(ContentWidth, ContentHeight);
            Canvas.SetLeft(child, item.X);
            Canvas.SetTop(child, item.Y);
        }
    }

    private void SyncTilePositionsFromModel()
    {
        foreach (var child in ItemCanvas.Children.OfType<FrameworkElement>())
        {
            if (child.Tag is not BlockItem item)
            {
                continue;
            }

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
        var opacity = emphasized || _dragging || _resizing ? 0.96 : 0.28;
        WidgetSurfaceStyle.FadeOpacity(DragBar, opacity, emphasized ? 140 : 220);
        WidgetSurfaceStyle.FadeOpacity(ResizeHandle, opacity, emphasized ? 140 : 220);
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
        ArrangeItemsEvenly();
        SyncTilePositionsFromModel();
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

    private void ArrangeButton_Click(object sender, RoutedEventArgs e)
    {
        ArrangeItemsEvenly();
        SyncTilePositionsFromModel();
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke($"Arranged {_block.Items.Count} icon(s) in '{_block.Name}'.");
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) =>
        _onDeleteRequested(_block);

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            // Prefer Move so Explorer can remove Desktop shortcuts after a successful intake move.
            e.AcceptedOperation = DataPackageOperation.Move | DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Move into Block";
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

        var added = 0;
        var moved = 0;
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

            var itemId = Guid.NewGuid();
            var intake = _intake.TryIntake(normalized, _block.Id, itemId);
            if (!intake.Succeeded || string.IsNullOrWhiteSpace(intake.TargetPath))
            {
                _onStatus?.Invoke(intake.ErrorMessage ?? "Could not add item.");
                continue;
            }

            if (!BlockTargetValidator.TryValidate(intake.TargetPath, type, out var target, out var targetError))
            {
                _onStatus?.Invoke(targetError);
                continue;
            }

            if (_block.Items.Any(i => string.Equals(i.Target, target, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Re-infer type from final target (moved .lnk stays Shortcut).
            type = BlockTargetValidator.InferType(target, Directory.Exists(target));
            var iconPath = _icons.TryGetCachedIconPath(target) ?? string.Empty;
            var item = new BlockItem
            {
                Id = itemId,
                Name = BlockTargetValidator.InferDisplayName(
                    intake.MovedFromSource ? normalized : target),
                Type = type,
                Target = target,
                Icon = iconPath,
                X = -1,
                Y = -1
            };

            // Preserve friendly name from original Desktop shortcut file name.
            if (intake.MovedFromSource)
            {
                item.Name = BlockTargetValidator.InferDisplayName(normalized);
            }

            _block.Items.Add(item);
            added++;
            if (intake.MovedFromSource)
            {
                moved++;
            }
            else if (!string.IsNullOrWhiteSpace(intake.ErrorMessage))
            {
                _onStatus?.Invoke(intake.ErrorMessage);
            }
        }

        if (added > 0)
        {
            e.AcceptedOperation = moved > 0 ? DataPackageOperation.Move : DataPackageOperation.Copy;
            RefreshItems(arrangeIfNeeded: true);
            _onLayoutCommitted();
            _onBoundsChanged?.Invoke();
            var msg = moved > 0
                ? $"Moved {moved} shortcut(s) into '{_block.Name}' (removed from Desktop)."
                : $"Linked {added} item(s) into '{_block.Name}'.";
            _onStatus?.Invoke(msg);
        }
    }
}
