using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SecretBase.App.Desktop;

/// <summary>
/// Transparent SystemBackdrop for wallpaper visibility.
/// Uses an ABI cast between Microsoft.UI.Composition and Windows.UI.Composition brush
/// projections required by WASDK 2.3's ICompositionSupportsSystemBackdrop.
/// </summary>
public sealed class TransparentSystemBackdrop : SystemBackdrop
{
    private CompositionColorBrush? _brush;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        if (xamlRoot.Content is not UIElement content)
        {
            base.OnTargetConnected(connectedTarget, xamlRoot);
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(content).Compositor;
        _brush ??= compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));

        // WASDK 2.3 projects SystemBackdrop as Windows.UI.Composition.CompositionBrush while
        // XAML visuals expose Microsoft.UI.Composition brushes. They share the WinRT ABI.
        connectedTarget.SystemBackdrop = (Windows.UI.Composition.CompositionBrush)(object)_brush;
        base.OnTargetConnected(connectedTarget, xamlRoot);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        disconnectedTarget.SystemBackdrop = null;
        _brush?.Dispose();
        _brush = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }
}
