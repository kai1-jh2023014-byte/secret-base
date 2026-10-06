namespace SecretBase.App.Desktop;

/// <summary>
/// Physical-pixel display metrics captured from AppWindow / DisplayArea
/// at the moment a display change is detected (MainWindow → DesktopPage).
/// </summary>
public readonly struct DisplayMetricsHint
{
    public DisplayMetricsHint(
        string reason,
        string displayId,
        int workX,
        int workY,
        int workWidth,
        int workHeight,
        int boundsX,
        int boundsY,
        int boundsWidth,
        int boundsHeight,
        int windowX,
        int windowY,
        int windowWidth,
        int windowHeight)
    {
        Reason = reason;
        DisplayId = displayId;
        WorkX = workX;
        WorkY = workY;
        WorkWidth = workWidth;
        WorkHeight = workHeight;
        BoundsX = boundsX;
        BoundsY = boundsY;
        BoundsWidth = boundsWidth;
        BoundsHeight = boundsHeight;
        WindowX = windowX;
        WindowY = windowY;
        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
    }

    public string Reason { get; }
    public string DisplayId { get; }
    public int WorkX { get; }
    public int WorkY { get; }
    public int WorkWidth { get; }
    public int WorkHeight { get; }
    public int BoundsX { get; }
    public int BoundsY { get; }
    public int BoundsWidth { get; }
    public int BoundsHeight { get; }
    public int WindowX { get; }
    public int WindowY { get; }
    public int WindowWidth { get; }
    public int WindowHeight { get; }
}
