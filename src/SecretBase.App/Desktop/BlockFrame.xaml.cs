using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
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
    private Button StyleButton = null!;
    private bool _itemDragging;
    private Point _itemLastPoint;
    private Point _itemPressPoint;
    private BlockItemTileMetrics _tileMetrics = BlockItemTileMetrics.ForGrid(showLabels: true);

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

        // Built in code (not XAML) so the WinUI markup compiler cannot NRE on new chrome.
        InsertStyleButton();

        Width = block.Size.Width;
        Height = block.Size.Height;
        ApplyTheme(theme);
        RefreshItems(arrangeIfNeeded: true);
        SetChromeEmphasis(emphasized: false);
        SizeChanged += (_, _) =>
        {
            // Rebuild tiles so icon scale tracks Block size (avoid overlap when cramped).
            RefreshItems(arrangeIfNeeded: true);
            _onBoundsChanged?.Invoke();
        };
    }

    public Guid BlockId => _block.Id;

    public Block Block => _block;

    private const double HeaderIconButtonSize = 20;
    private const double HeaderIconScale = 0.62;

    private void InsertStyleButton()
    {
        StyleButton = new Button
        {
            Content = CreateCompactHeaderIcon(Symbol.Pictures),
            Margin = new Thickness(0, 0, 2, 0)
        };
        SizeHeaderIconButton(StyleButton);
        StyleButton.Click += StyleButton_Click;
        ToolTipService.SetToolTip(StyleButton, "Icon style: white silhouette / fashion rail");

        // Name | Style | Arrange | Del — compact white silhouette icons.
        HeaderRow.ColumnDefinitions.Insert(1, new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(ArrangeButton, 2);
        Grid.SetColumn(DeleteButton, 3);
        Grid.SetColumn(StyleButton, 1);
        HeaderRow.Children.Insert(1, StyleButton);
        SizeHeaderIconButton(ArrangeButton);
        SizeHeaderIconButton(DeleteButton);
    }

    private static void SizeHeaderIconButton(Button button)
    {
        button.Width = HeaderIconButtonSize;
        button.Height = HeaderIconButtonSize;
        button.MinWidth = HeaderIconButtonSize;
        button.MinHeight = HeaderIconButtonSize;
        button.Padding = new Thickness(0);
        button.CornerRadius = new CornerRadius(6);
    }

    private static SymbolIcon CreateCompactHeaderIcon(Symbol symbol)
    {
        return new SymbolIcon
        {
            Symbol = symbol,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
            RenderTransform = new ScaleTransform
            {
                ScaleX = HeaderIconScale,
                ScaleY = HeaderIconScale
            }
        };
    }

    private static void TintHeaderIcon(Button button, ThemeDefinition theme, double opacity = 0.92)
    {
        var brush = ThemePainter.Brush(theme.WidgetForeground, opacity);
        button.Foreground = brush;
        if (button.Content is SymbolIcon icon)
        {
            icon.Foreground = brush;
        }
    }

    /// <summary>White silhouette / fashion rail — clear until hover glass.</summary>
    private bool UsesClearSurface =>
        BlockIconStyle.IsSilhouette(_block.IconStyle) || BlockLayoutMode.IsRail(_block.LayoutMode);

    private void ApplyTheme(ThemeDefinition theme)
    {
        var rail = BlockLayoutMode.IsRail(_block.LayoutMode);
        var radius = Math.Max(rail ? 10 : 14, theme.CornerRadius - (rail ? 6 : 0));
        DragBar.CornerRadius = new CornerRadius(radius, radius, 0, 0);
        Surface.CornerRadius = new CornerRadius(0, 0, radius, radius);
        Surface.Padding = rail ? new Thickness(4, 2, 4, 4) : new Thickness(8, 4, 8, 8);
        ApplySurfaceFill(emphasized: _pointerInside || _dragging || _resizing);
        NameText.Text = _block.Name;
        NameText.Foreground = ThemePainter.Brush(theme.WidgetForeground);
        NameText.FontFamily = new FontFamily(theme.FontFamily);
        NameText.CharacterSpacing = 20;
        NameText.Opacity = rail ? 0.45 : 0.9;
        EmptyHint.Foreground = ThemePainter.Brush(theme.ForegroundMuted);
        EmptyHint.FontFamily = new FontFamily(theme.FontFamily);
        WidgetSurfaceStyle.ApplyIconButton(DeleteButton, theme);
        WidgetSurfaceStyle.ApplyIconButton(ArrangeButton, theme);
        WidgetSurfaceStyle.ApplyIconButton(StyleButton, theme);
        // ApplyIconButton pads for shelf chips — keep Block header chrome compact.
        SizeHeaderIconButton(DeleteButton);
        SizeHeaderIconButton(ArrangeButton);
        SizeHeaderIconButton(StyleButton);
        StyleButton.Content = CreateCompactHeaderIcon(
            BlockIconStyle.IsSilhouette(_block.IconStyle) ? Symbol.OutlineStar : Symbol.Pictures);
        ArrangeButton.Content = CreateCompactHeaderIcon(rail ? Symbol.List : Symbol.ViewAll);
        DeleteButton.Content = CreateCompactHeaderIcon(Symbol.Delete);
        TintHeaderIcon(StyleButton, theme, 0.9);
        TintHeaderIcon(ArrangeButton, theme, 0.9);
        TintHeaderIcon(DeleteButton, theme, 0.9);
        ToolTipService.SetToolTip(
            StyleButton,
            BlockIconStyle.IsSilhouette(_block.IconStyle)
                ? "White silhouette icons — change style / layout"
                : "Color icons — change style / layout");
        ToolTipService.SetToolTip(
            ArrangeButton,
            rail ? "Fashion rail — arrange icons" : "Grid layout — arrange icons evenly");
        ResizeHandle.Background = ThemePainter.Brush(theme.Border, rail ? 0.2 : 0.35);

        var grip = ThemePainter.ParseColor(theme.WidgetForeground);
        grip.A = (byte)(rail ? 0x08 : 0x10);
        DragBar.Background = new SolidColorBrush(grip);
        SetChromeEmphasis(_pointerInside || _dragging || _resizing);
    }

    private void ApplySurfaceFill(bool emphasized)
    {
        var rail = IsRail;
        var clear = UsesClearSurface;
        if (clear && !emphasized)
        {
            Surface.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Surface.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Surface.BorderThickness = new Thickness(0);
            return;
        }

        // Hover (or always-on for color grid): airy launchpad glass.
        var blockOpacity = rail
            ? Math.Clamp(ThemePainter.EffectiveWidgetOpacity(_theme) * 0.10, 0.03, 0.14)
            : Math.Clamp(ThemePainter.SoftSurfaceOpacity(_theme), 0.12, 0.36);
        Surface.Background = ThemePainter.Brush(_theme.WidgetBackground, blockOpacity);
        Surface.BorderBrush = ThemePainter.Brush(_theme.Border, rail ? 0.08 : 0.18);
        Surface.BorderThickness = rail ? new Thickness(0) : new Thickness(1, 0, 1, 1);
    }

    private bool IsRail => BlockLayoutMode.IsRail(_block.LayoutMode);

    private bool LabelsVisible => !IsRail && _block.ShowLabels;

    private double TileW => _tileMetrics.TileWidth;

    private double TileH => _tileMetrics.TileHeight;

    private double ContentWidth =>
        ItemCanvas.ActualWidth > 0 ? ItemCanvas.ActualWidth : Math.Max(TileW, _block.Size.Width - 24);

    private double ContentHeight =>
        ItemCanvas.ActualHeight > 0 ? ItemCanvas.ActualHeight : Math.Max(TileH, _block.Size.Height - 60);

    private void ArrangeItemsEvenly()
    {
        if (IsRail)
        {
            _tileMetrics = BlockItemLayout.ArrangeRail(_block.Items, ContentWidth, ContentHeight);
            return;
        }

        _tileMetrics = BlockItemLayout.ArrangeEvenly(
            _block.Items,
            ContentWidth,
            ContentHeight,
            showLabels: _block.ShowLabels);
    }

    private void RefreshItems(bool arrangeIfNeeded)
    {
        if (arrangeIfNeeded || _block.Items.Any(i => !i.HasPlacement))
        {
            ArrangeItemsEvenly();
        }
        else if (_tileMetrics.TileWidth <= 0 || _tileMetrics.TileHeight <= 0)
        {
            _tileMetrics = IsRail
                ? BlockItemTileMetrics.ForRail()
                : BlockItemTileMetrics.ForGrid(_block.ShowLabels);
        }

        ItemCanvas.Children.Clear();
        foreach (var item in _block.Items)
        {
            item.ClampPlacement(ContentWidth, ContentHeight, TileW, TileH);
            ItemCanvas.Children.Add(CreateItemTile(item));
        }

        EmptyHint.Visibility = _block.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement CreateItemTile(BlockItem item)
    {
        var rail = IsRail;
        var showLabel = LabelsVisible;
        var silhouette = BlockIconStyle.IsSilhouette(_block.IconStyle);
        var iconSize = _tileMetrics.IconSize;
        var hostSize = _tileMetrics.IconHostSize;
        var labelFont = Math.Clamp(11 * _tileMetrics.Scale, 8, 11);

        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(item.Name) ? "Item" : item.Name,
            FontSize = labelFont,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 2,
            Foreground = ThemePainter.Brush(_theme.WidgetForeground),
            FontFamily = new FontFamily(_theme.FontFamily),
            Visibility = showLabel ? Visibility.Visible : Visibility.Collapsed
        };

        var iconHost = new Border
        {
            Width = hostSize,
            Height = hostSize,
            CornerRadius = new CornerRadius(rail ? 0 : Math.Max(4, 8 * _tileMetrics.Scale)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
        };

        // Custom icons are user imports under AppData. Missing files fall back for display
        // only — do not clear item.Icon here (that wiped icons when a file was briefly locked).
        var customPath = ResolveCustomIconPath(item);
        // Prefer 128px shell cache so DPI / 36px display stays sharp.
        var displayPath = customPath ?? _icons.TryGetCachedIconPath(item.Target, sizePx: 128);
        UIElement? iconVisual = silhouette
            ? IconSilhouettePainter.TryCreate(displayPath ?? string.Empty, iconSize)
            : TryCreateIconImage(displayPath, iconSize);
        if (iconVisual is null && silhouette && !string.IsNullOrWhiteSpace(displayPath))
        {
            // Fall back to color decode if silhouette conversion fails.
            iconVisual = TryCreateIconImage(displayPath, iconSize);
        }

        if (iconVisual is not null)
        {
            iconHost.Child = iconVisual;
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
                FontSize = Math.Clamp(rail ? 9 : 10 * _tileMetrics.Scale, 7, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = silhouette
                    ? ThemePainter.Brush("#FFFFFFFF", 0.9)
                    : ThemePainter.Brush(_theme.ForegroundMuted)
            };
        }

        var stack = new StackPanel
        {
            Width = TileW,
            Spacing = showLabel ? Math.Max(2, 4 * _tileMetrics.Scale) : 0,
            Padding = new Thickness(rail || !showLabel ? 0 : 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(iconHost);
        if (showLabel)
        {
            stack.Children.Add(label);
        }

        var tile = new Border
        {
            Width = TileW,
            Height = TileH,
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            CornerRadius = new CornerRadius(rail ? 0 : Math.Max(4, 8 * _tileMetrics.Scale)),
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

        var renameItem = new MenuFlyoutItem { Text = "Rename…" };
        renameItem.Click += async (_, _) => await RenameItemAsync(item);
        menu.Items.Add(renameItem);

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

            item.ClampPlacement(ContentWidth, ContentHeight, TileW, TileH);
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
        _activeItem.ClampPlacement(ItemCanvas.ActualWidth, ItemCanvas.ActualHeight, TileW, TileH);
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

    private async Task RenameItemAsync(BlockItem item)
    {
        var box = new TextBox
        {
            Header = "App name",
            Text = item.Name,
            PlaceholderText = "Display name",
            MaxLength = 80,
            MinWidth = 280
        };

        var dialog = new ContentDialog
        {
            Title = "Rename",
            Content = box,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result;
        using (_dialogInput?.Enter())
        {
            result = await dialog.ShowAsync();
        }

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var next = (box.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(next))
        {
            _onStatus?.Invoke("Name cannot be empty.");
            return;
        }

        if (string.Equals(item.Name, next, StringComparison.Ordinal))
        {
            return;
        }

        item.Name = next;
        RefreshItems(arrangeIfNeeded: false);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke($"Renamed to '{item.Name}'.");
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

    private static Image? TryCreateIconImage(string? path, double displaySize = 36)
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
            bitmap.DecodePixelWidth = (int)Math.Clamp(displaySize * 2, 48, 128);
            bitmap.UriSource = new Uri($"{uri.AbsoluteUri}?v={cacheBust}", UriKind.Absolute);
            return new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                Width = displaySize,
                Height = displaySize
            };
        }
        catch
        {
            return null;
        }
    }

    private void StyleButton_Click(object sender, RoutedEventArgs e)
    {
        var flyout = new MenuFlyout();

        var colorItem = new ToggleMenuFlyoutItem
        {
            Text = "Color icons",
            IsChecked = !BlockIconStyle.IsSilhouette(_block.IconStyle)
        };
        colorItem.Click += (_, _) => ApplyIconStyle(BlockIconStyle.Color);

        var whiteItem = new ToggleMenuFlyoutItem
        {
            Text = "White silhouette",
            IsChecked = BlockIconStyle.IsSilhouette(_block.IconStyle)
        };
        whiteItem.Click += (_, _) => ApplyIconStyle(BlockIconStyle.Silhouette);

        var gridItem = new ToggleMenuFlyoutItem
        {
            Text = "Grid layout",
            IsChecked = !IsRail
        };
        gridItem.Click += (_, _) => ApplyLayoutMode(BlockLayoutMode.Grid);

        var railItem = new ToggleMenuFlyoutItem
        {
            Text = "Fashion rail",
            IsChecked = IsRail
        };
        railItem.Click += (_, _) => ApplyLayoutMode(BlockLayoutMode.Rail);

        var labelsItem = new ToggleMenuFlyoutItem
        {
            Text = "Show icon names",
            IsChecked = _block.ShowLabels,
            IsEnabled = !IsRail
        };
        labelsItem.Click += (_, _) => ApplyShowLabels(!_block.ShowLabels);

        flyout.Items.Add(colorItem);
        flyout.Items.Add(whiteItem);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(gridItem);
        flyout.Items.Add(railItem);
        flyout.Items.Add(labelsItem);
        flyout.ShowAt(StyleButton);
    }

    private void ApplyShowLabels(bool show)
    {
        _block.ShowLabels = show;
        RefreshItems(arrangeIfNeeded: true);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke(
            show
                ? $"Icon names shown on '{_block.Name}'."
                : $"Icon names hidden on '{_block.Name}'.");
    }

    private void ApplyIconStyle(string style)
    {
        _block.IconStyle = BlockIconStyle.Normalize(style);
        ApplyTheme(_theme);
        RefreshItems(arrangeIfNeeded: false);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke(
            BlockIconStyle.IsSilhouette(_block.IconStyle)
                ? $"White silhouette icons on '{_block.Name}'."
                : $"Color icons on '{_block.Name}'.");
    }

    private void ApplyLayoutMode(string mode)
    {
        _block.LayoutMode = BlockLayoutMode.Normalize(mode);
        if (IsRail)
        {
            // Slim fashion strip — vertical when many icons, horizontal when wide.
            var count = Math.Max(1, _block.Items.Count);
            var vertical = _block.Size.Height >= _block.Size.Width || count > 4;
            if (vertical)
            {
                _block.Size.Width = Math.Max(Block.RailMinWidth, 88);
                _block.Size.Height = Math.Max(
                    Block.RailMinHeight,
                    48 + count * (BlockItem.RailTileHeight + 10));
            }
            else
            {
                _block.Size.Width = Math.Max(
                    Block.RailMinWidth,
                    24 + count * (BlockItem.RailTileWidth + 10));
                _block.Size.Height = Math.Max(Block.RailMinHeight, 96);
            }
        }
        else if (_block.Size.Width < Block.MinWidth || _block.Size.Height < Block.MinHeight)
        {
            _block.Size.Width = Math.Max(_block.Size.Width, Block.DefaultWidth);
            _block.Size.Height = Math.Max(_block.Size.Height, Block.DefaultHeight);
        }

        _block.ClampSize();
        Width = _block.Size.Width;
        Height = _block.Size.Height;
        ApplyTheme(_theme);
        RefreshItems(arrangeIfNeeded: true);
        _onLayoutCommitted();
        _onBoundsChanged?.Invoke();
        _onStatus?.Invoke(
            IsRail
                ? $"Fashion rail layout on '{_block.Name}'."
                : $"Grid layout on '{_block.Name}'.");
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
        var active = emphasized || _dragging || _resizing;
        var opacity = active ? 0.96 : 0.28;
        WidgetSurfaceStyle.FadeOpacity(DragBar, opacity, emphasized ? 140 : 220);
        WidgetSurfaceStyle.FadeOpacity(ResizeHandle, opacity, emphasized ? 140 : 220);

        if (UsesClearSurface)
        {
            ApplySurfaceFill(active);
            // White / rail: header chrome stays out of the way until hover.
            var headerOpacity = active ? 1.0 : 0.0;
            WidgetSurfaceStyle.FadeOpacity(HeaderRow, headerOpacity, emphasized ? 140 : 220);
        }
        else
        {
            HeaderRow.Opacity = 1.0;
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

        var newX = Canvas.GetLeft(this) + dx;
        var newY = Canvas.GetTop(this) + dy;
        ClampToParentCanvas(ref newX, ref newY, Width, Height);
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
        if (Parent is Canvas canvas && canvas.ActualWidth > 0 && canvas.ActualHeight > 0)
        {
            var maxW = Math.Max(Block.MinWidth, canvas.ActualWidth - Canvas.GetLeft(this));
            var maxH = Math.Max(
                Block.MinHeight,
                canvas.ActualHeight - Canvas.GetTop(this) - DesktopViewportLayout.DefaultBottomReserve);
            _block.Size.Clamp(Block.MinWidth, Block.MinHeight, maxW, maxH);
        }

        Width = _block.Size.Width;
        Height = _block.Size.Height;
        RefreshItems(arrangeIfNeeded: true);
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

        RefreshItems(arrangeIfNeeded: true);
        SetChromeEmphasis(emphasized: _pointerInside);
        _onBoundsChanged?.Invoke();
        _onLayoutCommitted();
        e.Handled = true;
    }

    private void ArrangeButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshItems(arrangeIfNeeded: true);
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
