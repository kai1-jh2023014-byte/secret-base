using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// macOS v1 is a normal workspace window (Avalonia), not a wallpaper overlay.
/// Does not replace Dock, menu bar, or Finder. Hit-testing / region shaping is a no-op.
/// </summary>
public sealed class MacWorkspaceWindowService : IDesktopOverlayService
{
    public void ApplyChromelessWorkAreaOverlay(DesktopOverlayTarget target)
    {
        _ = target;
    }

    public void KeepBehindApplicationWindows(DesktopOverlayTarget target)
    {
        _ = target;
    }

    public void UpdateInteractiveInputRegions(
        DesktopOverlayTarget target,
        IReadOnlyList<OverlayInputRect> rects)
    {
        _ = target;
        _ = rects;
    }
}
