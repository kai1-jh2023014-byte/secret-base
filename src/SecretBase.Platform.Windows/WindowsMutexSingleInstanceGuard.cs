using System.Threading;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Process-wide single-instance guard using a named mutex (no elevation, per-user Local namespace).
/// </summary>
public sealed class WindowsMutexSingleInstanceGuard : ISingleInstanceGuard
{
    public const string DefaultMutexName = @"Local\SecretBase.App.SingleInstance";

    private readonly string _mutexName;
    private Mutex? _mutex;
    private bool _held;

    public WindowsMutexSingleInstanceGuard(string? mutexName = null)
    {
        _mutexName = string.IsNullOrWhiteSpace(mutexName) ? DefaultMutexName : mutexName;
    }

    public bool IsHeld => _held;

    public bool TryAcquire()
    {
        if (_held)
        {
            return true;
        }

        try
        {
            var mutex = new Mutex(initiallyOwned: true, name: _mutexName, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }

            _mutex = mutex;
            _held = true;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_held)
        {
            return;
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Mutex not owned — already released or never acquired.
        }
        finally
        {
            _mutex?.Dispose();
            _mutex = null;
            _held = false;
        }
    }
}
