namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Client-area rectangle in physical pixels (not DIP) for overlay input shaping.
/// Origin is the overlay window's client top-left.
/// </summary>
public readonly record struct OverlayInputRect(int X, int Y, int Width, int Height);
