namespace SecretBase.Platform.Windows;

/// <summary>
/// Lets a second desktop-shortcut launch ask the already-running overlay to show itself
/// instead of exiting with no visible result.
/// </summary>
public sealed class WindowsInstanceActivation : IDisposable
{
    public const string DefaultEventName = @"Local\SecretBase.App.Activate";

    private readonly EventWaitHandle _signal;
    private readonly ManualResetEvent _stop = new(false);
    private readonly Thread _thread;
    private bool _disposed;

    private WindowsInstanceActivation(string eventName, Action onActivate)
    {
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        _thread = new Thread(() =>
        {
            WaitHandle[] handles = [_signal, _stop];
            while (true)
            {
                int index;
                try
                {
                    index = WaitHandle.WaitAny(handles);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (index != 0)
                {
                    return;
                }

                try
                {
                    onActivate();
                }
                catch
                {
                    // The UI callback owns its own errors.
                }
            }
        })
        {
            IsBackground = true,
            Name = "SecretBase.Activate"
        };
        _thread.Start();
    }

    public static bool Signal(string? eventName = null)
    {
        try
        {
            using var handle = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                eventName ?? DefaultEventName);
            handle.Set();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    public static WindowsInstanceActivation Listen(Action onActivate, string? eventName = null)
    {
        ArgumentNullException.ThrowIfNull(onActivate);
        return new WindowsInstanceActivation(eventName ?? DefaultEventName, onActivate);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Set();
        _thread.Join(TimeSpan.FromMilliseconds(300));
        _signal.Dispose();
        _stop.Dispose();
    }
}
