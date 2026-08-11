namespace SecretBase.Core.Widgets;

public sealed class WidgetSize
{
    public double Width { get; set; }
    public double Height { get; set; }

    public WidgetSize()
    {
    }

    public WidgetSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public void Clamp(double minWidth, double minHeight, double? maxWidth = null, double? maxHeight = null)
    {
        Width = Math.Max(minWidth, Width);
        Height = Math.Max(minHeight, Height);
        if (maxWidth is double maxW)
        {
            Width = Math.Min(maxW, Width);
        }

        if (maxHeight is double maxH)
        {
            Height = Math.Min(maxH, Height);
        }
    }
}
