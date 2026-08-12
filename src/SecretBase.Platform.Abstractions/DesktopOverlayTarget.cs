namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Identifies the WinUI / AppWindow host to configure as a desktop overlay.
/// Carries only IDs/handles so Core/Abstractions stay free of WinUI types.
/// </summary>
public sealed record DesktopOverlayTarget(
    ulong AppWindowId,
    nint WindowHandle);
