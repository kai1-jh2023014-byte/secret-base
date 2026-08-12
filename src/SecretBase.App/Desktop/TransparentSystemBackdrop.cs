using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SecretBase.App.Desktop;

/// <summary>
/// Fully transparent SystemBackdrop using the public WinUI SystemBackdrop extension point.
/// Paired with documented DWM frame extension in Platform.Windows so empty areas can show the wallpaper.
/// </summary>
public sealed class TransparentSystemBackdrop : SystemBackdrop
{
    private CompositionColorBrush? _brush;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        if (xamlRoot.Content is not UIElement content)
        {
            return;
        }

        var compositor = ElementCompositionPreview.GetElementVisual(content).Compositor;
        _brush ??= compositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
        connectedTarget.SystemBackdrop = _brush;
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
