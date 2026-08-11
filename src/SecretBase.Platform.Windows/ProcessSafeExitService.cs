using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Process-local exit. Does not touch Explorer, Taskbar, registry shell keys, or startup hooks.
/// </summary>
public sealed class ProcessSafeExitService : ISafeExitService
{
    private readonly Action _exitAction;

    public ProcessSafeExitService(Action? exitAction = null)
    {
        _exitAction = exitAction ?? (() => Environment.Exit(0));
    }

    public void RequestExit() => _exitAction();
}
