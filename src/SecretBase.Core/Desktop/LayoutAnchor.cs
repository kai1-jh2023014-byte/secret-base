namespace SecretBase.Core.Desktop;

/// <summary>
/// Soft placement bias inferred from a rect inside the safe area.
/// Used by <see cref="ResponsiveLayoutResolver"/> — not persisted on widgets.
/// </summary>
public enum LayoutAnchor
{
    TopLeft,
    TopCenter,
    TopRight,
    Center,
    BottomLeft,
    BottomCenter,
    BottomRight
}
