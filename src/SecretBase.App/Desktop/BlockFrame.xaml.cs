using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SecretBase.Core.Blocks;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Theming;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace SecretBase.App.Desktop;

/// <summary>
/// Desktop Block chrome: move / resize / name / delete / item grid + drop targets.
/// Mirrors <see cref="WidgetFrame"/> patterns without shared base (one impl first).
/// </summary>
public sealed partial class BlockFrame : UserControl
{
    private readonly Block _block;
    private readonly ThemeDefinition _theme;
    private readonly ITargetLaunchService _launcher;
    private readonly Action _onLayoutCommitted;
    private readonly Action? _onBoundsChanged;
    private readonly Action<Block> _onDeleteRequested;
    private readonly Action<string>? _onStatus;

    private bool _dragging;
    private bool _resizing;
    private bool _pointerInside;
    private Point _lastPoint;

    public BlockFrame(
        Block block,
        ThemeDefinition theme,
        ITargetLaunchService launcher,
        Action onLayoutCommitted,
        Action<Block> onDeleteRequested,
        Action? onBoundsChanged = null,
        Action<string>? onStatus = null)
    {
        InitializeComponent();
        _block = block;
        _theme = theme;
        _launcher = launcher;
        _onLayoutCommitted = onLayoutCommitted;
        _onDeleteRequested = onDeleteRequested;
        _onBoundsChanged = onBoundsChanged;
        _onStatus = onStatus;

        Width = block.Size.Width;
        Height = block.Size.Height;
        ApplyTheme(theme);
        RefreshItems();
        SetChromeEmphasis(emphasized: false);
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
        var vms = _block.Items
            .Select(ItemVm.From)
            .ToList();
        ItemsGrid.ItemsSource = vms;
        EmptyHint.Visibility = vms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

    private void ItemsGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ItemVm vm)
        {
            return;
        }

        var result = _launcher.TryLaunch(new TargetLaunchRequest(
            Target: vm.Target,
            ItemType: vm.Type.ToString(),
            DisplayName: vm.Name));

        if (!result.Succeeded)
        {
            _onStatus?.Invoke(result.ErrorMessage ?? "Launch failed.");
        }
    }

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

        var added = 0;
        foreach (var storageItem in items)
        {
            var path = storageItem.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var isDirectory = storageItem is StorageFolder
                             || (storageItem is StorageFile && Directory.Exists(path));
            // StorageFile for folders is rare; prefer StorageFolder check.
            if (storageItem is StorageFolder)
            {
                isDirectory = true;
            }
            else if (storageItem is StorageFile)
            {
                isDirectory = false;
            }

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

            _block.Items.Add(new BlockItem
            {
                Id = Guid.NewGuid(),
                Name = BlockTargetValidator.InferDisplayName(normalized),
                Type = type,
                Target = normalized,
                Icon = string.Empty
            });
            added++;
        }

        if (added > 0)
        {
            RefreshItems();
            _onLayoutCommitted();
            _onBoundsChanged?.Invoke();
            _onStatus?.Invoke($"Added {added} item(s) to '{_block.Name}'.");
        }
    }

    private sealed class ItemVm
    {
        public required string Name { get; init; }
        public required string Target { get; init; }
        public required BlockItemType Type { get; init; }
        public required string Glyph { get; init; }

        public static ItemVm From(BlockItem item) => new()
        {
            Name = string.IsNullOrWhiteSpace(item.Name) ? "Item" : item.Name,
            Target = item.Target,
            Type = item.Type,
            Glyph = GlyphFor(item.Type)
        };

        private static string GlyphFor(BlockItemType type) => type switch
        {
            BlockItemType.Application => "APP",
            BlockItemType.Shortcut => "LNK",
            BlockItemType.Folder => "DIR",
            _ => "FILE"
        };
    }
}
