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

    /// <summary>
    /// Set when <see cref="TryAcquire"/> fails because the mutex could not be opened,
    /// rather than because another live instance holds it.
    /// </summary>
    public string? FailureMessage { get; private set; }

    public bool TryAcquire()
    {
        if (_held)
        {
            return true;
        }

        FailureMessage = null;
        try
        {
            var mutex = new Mutex(initiallyOwned: false, name: _mutexName);
            try
            {
                if (!mutex.WaitOne(TimeSpan.Zero))
                {
                    mutex.Dispose();
                    return false;
                }
            }
            catch (AbandonedMutexException)
            {
                // The previous owner exited without releasing. This process owns the mutex now.
            }

            _mutex = mutex;
            _held = true;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            FailureMessage = ex.Message;
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
