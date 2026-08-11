namespace SecretBase.Core.Widgets;

public sealed class WidgetPosition
{
    public double X { get; set; }
    public double Y { get; set; }

    public WidgetPosition()
    {
    }

    public WidgetPosition(double x, double y)
    {
        X = x;
        Y = y;
    }
}
