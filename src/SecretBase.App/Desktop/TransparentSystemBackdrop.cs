using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinCompositor = Windows.UI.Composition.Compositor;

namespace SecretBase.App.Desktop;

/// <summary>
/// Fully transparent SystemBackdrop so wallpaper shows through empty overlay regions.
/// Uses <see cref="Windows.UI.Composition.Compositor"/> (not Microsoft.UI) so the brush type
/// matches <see cref="ICompositionSupportsSystemBackdrop.SystemBackdrop"/> — no ABI cast.
/// Pattern aligned with documented WinUIEx TransparentTintBackdrop / CompositionBrushBackdrop.
/// </summary>
public sealed class TransparentSystemBackdrop : SystemBackdrop
{
    private static nint s_dispatcherQueueController;
    private static WinCompositor? s_compositor;
    private static readonly object s_compositorLock = new();

    private Windows.UI.Composition.CompositionColorBrush? _brush;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        _brush ??= SystemCompositor.CreateColorBrush(Color.FromArgb(0, 0, 0, 0));
        connectedTarget.SystemBackdrop = _brush;
        base.OnTargetConnected(connectedTarget, xamlRoot);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        var backdrop = disconnectedTarget.SystemBackdrop;
        disconnectedTarget.SystemBackdrop = null;
        backdrop?.Dispose();
        _brush = null;
        base.OnTargetDisconnected(disconnectedTarget);
    }

    private static WinCompositor SystemCompositor
    {
        get
        {
            if (s_compositor is not null)
            {
                return s_compositor;
            }

            lock (s_compositorLock)
            {
                if (s_compositor is null)
                {
                    EnsureSystemDispatcherQueue();
                    s_compositor = new WinCompositor();
                }

                return s_compositor;
            }
        }
    }

    private static void EnsureSystemDispatcherQueue()
    {
        // Acrylic/Mica/custom SystemBackdrop brushes require Windows.System.DispatcherQueue
        // on the UI thread (separate from Microsoft.UI.Dispatching.DispatcherQueue).
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is not null
            || s_dispatcherQueueController != nint.Zero)
        {
            return;
        }

        var options = new DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = 2, // DQTYPE_THREAD_CURRENT
            apartmentType = 2 // DQTAT_COM_STA
        };

        _ = CreateDispatcherQueueController(options, out s_dispatcherQueueController);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(
        [In] DispatcherQueueOptions options,
        out nint dispatcherQueueController);
}
