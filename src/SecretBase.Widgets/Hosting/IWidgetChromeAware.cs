namespace SecretBase.Widgets.Hosting;

/// <summary>
/// Optional contract for widget content that hides its own chrome (e.g. mode toggle)
/// until the host frame is selected or hovered.
/// </summary>
public interface IWidgetChromeAware
{
    void SetChromeVisible(bool visible);
}
