namespace SecretBase.Widgets.Hosting;

/// <summary>
/// Host expands the overlay hit-test region while a modal dialog is open,
/// then restores widget-only regions on dispose.
/// </summary>
public sealed class OverlayDialogInput
{
    private readonly Action? _begin;
    private readonly Action? _end;

    public OverlayDialogInput(Action? begin = null, Action? end = null)
    {
        _begin = begin;
        _end = end;
    }

    public IDisposable Enter()
    {
        _begin?.Invoke();
        return new Releaser(_end);
    }

    private sealed class Releaser(Action? end) : IDisposable
    {
        private bool _done;

        public void Dispose()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            end?.Invoke();
        }
    }
}
