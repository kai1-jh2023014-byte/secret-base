using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Blocks;
using SecretBase.Core.Themes;
using SecretBase.Platform.Abstractions;
using SecretBase.Widgets.Hosting;
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
    private readonly ICustomIconService? _customIcons;
    private readonly IPathPickService? _pathPicker;
    private readonly OverlayDialogInput? _dialogInput;
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
        Action<string>? onStatus = null,
        ICustomIconService? customIcons = null,
        IPathPickService? pathPicker = null,
        OverlayDialogInput? dialogInput = null)
    {
        InitializeComponent();
        _block = block;
        _theme = theme;
        _launcher = launcher;
        _icons = icons;
        _intake = intake;
        _customIcons = customIcons;
        _pathPicker = pathPicker;
        _dialogInput = dialogInput;
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
        var radius = Math.Max(14, theme.CornerRadius);
        DragBar.CornerRadius = new CornerRadius(radius, radius, 0, 0);
        Surface.CornerRadius = new CornerRadius(0, 0, radius, radius);
        // Launchpad-like glass: lighter fill so wallpaper reads through Blocks.
        var blockOpacity = Math.Clamp(ThemePainter.EffectiveWidgetOpacity(theme) * 0.42, 0.18, 0.48);
        Surface.Background = ThemePainter.Brush(theme.WidgetBackground, blockOpacity);
        Surface.BorderBrush = ThemePainter.Brush(theme.Border, 0.18);
        Surface.BorderThickness = new Thickness(1, 0, 1, 1);
        NameText.Text = _block.Name;
        NameText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        NameText.FontFamily = new FontFamily(theme.FontFamily);
        NameText.CharacterSpacing = 20;
        EmptyHint.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        EmptyHint.FontFamily = new FontFamily(theme.FontFamily);
        WidgetSurfaceStyle.ApplyGhostButton(DeleteButton, theme);
        WidgetSurfaceStyle.ApplyGhostButton(ArrangeButton, theme);
        DeleteButton.Opacity = 0.75;
        ArrangeButton.Opacity = 0.75;
        ResizeHandle.Background = ThemePainter.Brush(theme.Border, 0.35);

        var grip = ThemePainter.ParseColor(theme.WidgetForeground);
        grip.A = 0x10;
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
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };

        // Custom icons are user imports under AppData. Missing files fall back for display
        // only — do not clear item.Icon here (that wiped icons when a file was briefly locked).
        var customPath = ResolveCustomIconPath(item);
        // Prefer 128px shell cache so DPI / 36px display stays sharp.
        var displayPath = customPath ?? _icons.TryGetCachedIconPath(item.Target, sizePx: 128);
        var image = TryCreateIconImage(displayPath);
        if (image is not null)
        {
            iconHost.Child = image;
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
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            CornerRadius = new CornerRadius(8),
            Child = stack,
            Tag = item,
            CanDrag = false
        };

        tile.PointerEntered += ItemTile_PointerEntered;
        tile.PointerExited += ItemTile_PointerExited;
        tile.PointerPressed += ItemTile_PointerPressed;
        tile.PointerMoved += ItemTile_PointerMoved;
        tile.PointerReleased += ItemTile_PointerReleased;
        tile.PointerCaptureLost += ItemTile_PointerCaptureLost;
        ToolTipService.SetToolTip(tile, $"{item.Name}\n{item.Target}\nDrag to move · Click to open · Right-click for more");

        var menu = new MenuFlyout();
        var openItem = new MenuFlyoutItem { Text = "Open" };
        openItem.Click += (_, _) => LaunchItem(item);
        menu.Items.Add(openItem);

        if (_customIcons is not null)
        {
            var changeIcon = new MenuFlyoutItem { Text = "Change icon…" };
            changeIcon.Click += async (_, _) => await ChangeItemIconAsync(item);
            menu.Items.Add(changeIcon);

            if (_customIcons.IsUserIcon(item.Icon))
            {
                var resetIcon = new MenuFlyoutItem { Text = "Reset icon" };
                resetIcon.Click += (_, _) => ResetItemIcon(item);
                menu.Items.Add(resetIcon);
            }
        }

        if (item.HiddenFromDesktop)
        {
            var restoreItem = new MenuFlyoutItem { Text = "Return to Desktop" };
            restoreItem.Click += (_, _) => RestoreItemToDesktop(item);
            menu.Items.Add(restoreItem);
        }
        else
        {
            var removeItem = new MenuFlyoutItem { Text = "Remove from Block" };
            removeItem.Click += (_, _) => RemoveLinkedItem(item);
            menu.Items.Add(removeItem);
        }

        tile.ContextFlyout = menu;

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

    private void ItemTile_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border tile)
        {
            SetTileHighlight(tile, active: true);
        }
    }

    private void ItemTile_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border tile && !ReferenceEquals(tile, _activeTile))
        {
            SetTileHighlight(tile, active: false);
        }
    }

    private static void SetTileHighlight(Border tile, bool active)
    {
        tile.Background = active
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF))
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        if (tile.Child is StackPanel stack
            && stack.Children.OfType<Border>().FirstOrDefault() is { } iconHost)
        {
            iconHost.Background = active
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
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
        if (tile is Border border)
        {
            SetTileHighlight(border, active: true);
        }

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
        var tile = _activeTile;
        if (!captureLost && sender is UIElement el)
        {
            el.ReleasePointerCapture(e.Pointer);
        }

        _activeTile = null;
        _activeItem = null;
        _itemDragging = false;

        if (tile is Border border)
        {
            SetTileHighlight(border, active: false);
        }

        if (dragged)
        {
            _onLayoutCommitted();
            _onBoundsChanged?.Invoke();
        }
        else
        {
            LaunchItem(item);
        }

        e.Handled = true;
    }

    private void LaunchItem(BlockItem item)
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

    private void RestoreItemToDesktop(BlockItem item)
    {
        if (!item.HiddenFromDesktop)
        {
            RemoveLinkedItem(item);
            return;
        }

        if (!_intake.TryRestoreToDesktop(item.Target, item.DesktopOriginPath, out _, out var error))
        {
            _onStatus?.Invoke(error ?? "Could not return the item to Desktop. It stayed in the Block.");
            return;
        }

        DiscardUserIcon(item);
        _block.Items.Remove(item);
        RefreshItems(arrangeIfNeeded: true);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke($"Returned '{item.Name}' to the Desktop.");
    }

    private void RemoveLinkedItem(BlockItem item)
    {
        DiscardUserIcon(item);
        _block.Items.Remove(item);
        RefreshItems(arrangeIfNeeded: true);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke($"Removed '{item.Name}' from '{_block.Name}'. The original file was not deleted.");
    }

    private void DiscardUserIcon(BlockItem item)
    {
        if (_customIcons is null || !_customIcons.IsUserIcon(item.Icon))
        {
            return;
        }

        _customIcons.TryDeleteUserIcon(item.Icon);
        item.Icon = string.Empty;
    }

    private void ResetItemIcon(BlockItem item)
    {
        DiscardUserIcon(item);
        RefreshItems(arrangeIfNeeded: false);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke($"Reset icon for '{item.Name}'.");
    }

    private async Task ChangeItemIconAsync(BlockItem item)
    {
        if (_customIcons is null)
        {
            _onStatus?.Invoke("Icon customization is not available.");
            return;
        }

        var presets = BlockCustomIcons.Presets.ToList();
        var presetList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 240
        };
        foreach (var preset in presets)
        {
            var swatch = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(ParsePresetColor(preset.HexColor)),
                VerticalAlignment = VerticalAlignment.Center
            };
            presetList.Items.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                Children =
                {
                    swatch,
                    new TextBlock
                    {
                        Text = preset.DisplayName,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            });
        }

        if (presetList.Items.Count > 0)
        {
            presetList.SelectedIndex = 0;
        }

        var dialog = new ContentDialog
        {
            Title = $"Icon for {item.Name}",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Choose a design color, or import an image (PNG, JPG, BMP, GIF, ICO, WEBP, TIFF).",
                        FontSize = 12,
                        Opacity = 0.8,
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    presetList
                }
            },
            PrimaryButtonText = "Apply design",
            SecondaryButtonText = "Import image…",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        using (_dialogInput?.Enter())
        {
            result = await dialog.ShowAsync();
        }

        if (result == ContentDialogResult.Secondary)
        {
            await ImportItemIconAsync(item);
            return;
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (presetList.SelectedIndex < 0 || presetList.SelectedIndex >= presets.Count)
        {
            _onStatus?.Invoke("Select a design first.");
            return;
        }

        var presetId = presets[presetList.SelectedIndex].Id;
        var glyph = BlockCustomIcons.GlyphFor(item.Type, item.Name);
        if (!_customIcons.TryCreatePresetIcon(item.Id, presetId, glyph, out var stored, out var error)
            || string.IsNullOrWhiteSpace(stored))
        {
            _onStatus?.Invoke(error ?? "Could not create that icon design.");
            return;
        }

        ApplyStoredIcon(item, stored);
        _onStatus?.Invoke($"Updated icon for '{item.Name}'.");
    }

    private async Task ImportItemIconAsync(BlockItem item)
    {
        if (_customIcons is null || _pathPicker is null)
        {
            _onStatus?.Invoke("Image import is not available.");
            return;
        }

        PathPickResult pick;
        using (_dialogInput?.Enter())
        {
            pick = await _pathPicker.PickImageAsync();
        }

        if (pick.Cancelled)
        {
            return;
        }

        if (!pick.Succeeded || string.IsNullOrWhiteSpace(pick.Path))
        {
            _onStatus?.Invoke(pick.ErrorMessage ?? "Could not open the image picker.");
            return;
        }

        if (!_customIcons.TryImportImage(pick.Path, item.Id, out var stored, out var error)
            || string.IsNullOrWhiteSpace(stored))
        {
            _onStatus?.Invoke(error ?? "Could not import that image.");
            return;
        }

        ApplyStoredIcon(item, stored);
        _onStatus?.Invoke($"Imported custom icon for '{item.Name}'.");
    }

    private void ApplyStoredIcon(BlockItem item, string storedPath)
    {
        var previous = item.Icon;
        item.Icon = storedPath;
        if (!string.IsNullOrWhiteSpace(previous)
            && !string.Equals(previous, storedPath, StringComparison.OrdinalIgnoreCase)
            && _customIcons is not null
            && _customIcons.IsUserIcon(previous))
        {
            _customIcons.TryDeleteUserIcon(previous);
        }

        RefreshItems(arrangeIfNeeded: false);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
    }

    private string? ResolveCustomIconPath(BlockItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Icon))
        {
            return null;
        }

        if (_customIcons is not null && !_customIcons.IsUserIcon(item.Icon))
        {
            // Shell cache paths must not be treated as persisted custom icons.
            return null;
        }

        return File.Exists(item.Icon) ? item.Icon : null;
    }

    private static Image? TryCreateIconImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            // Bust WinUI's URI cache and avoid sharing one decoded bitmap across tiles.
            var uri = new Uri(path, UriKind.Absolute);
            var cacheBust = File.GetLastWriteTimeUtc(path).Ticks;
            var bitmap = new BitmapImage();
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            // Decode near display size at high quality; source PNGs are typically 96–128px.
            bitmap.DecodePixelType = DecodePixelType.Logical;
            bitmap.DecodePixelWidth = 72;
            bitmap.UriSource = new Uri($"{uri.AbsoluteUri}?v={cacheBust}", UriKind.Absolute);
            return new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                Width = 36,
                Height = 36
            };
        }
        catch
        {
            return null;
        }
    }

    private static Windows.UI.Color ParsePresetColor(string hex)
    {
        var value = hex.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        try
        {
            if (value.Length == 8)
            {
                return Windows.UI.Color.FromArgb(
                    Convert.ToByte(value[..2], 16),
                    Convert.ToByte(value[2..4], 16),
                    Convert.ToByte(value[4..6], 16),
                    Convert.ToByte(value[6..8], 16));
            }

            if (value.Length == 6)
            {
                return Windows.UI.Color.FromArgb(
                    255,
                    Convert.ToByte(value[..2], 16),
                    Convert.ToByte(value[2..4], 16),
                    Convert.ToByte(value[4..6], 16));
            }
        }
        catch
        {
            // Fall through.
        }

        return Windows.UI.Color.FromArgb(255, 47, 111, 237);
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
            var item = new BlockItem
            {
                Id = itemId,
                Name = BlockTargetValidator.InferDisplayName(
                    intake.MovedFromSource ? normalized : target),
                Type = type,
                Target = target,
                Icon = string.Empty,
                DesktopOriginPath = intake.DesktopOriginPath,
                HiddenFromDesktop = intake.MovedFromSource,
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
                ? $"Moved {moved} item(s) off the Desktop into '{_block.Name}'."
                : $"Linked {added} item(s) into '{_block.Name}'.";
            _onStatus?.Invoke(msg);
        }
    }
}
