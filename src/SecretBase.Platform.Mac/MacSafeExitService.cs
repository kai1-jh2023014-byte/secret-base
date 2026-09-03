using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Process-local exit. Does not touch Dock, Finder, launchd system jobs, or login items
/// beyond what the user already registered through <see cref="IAutoStartService"/>.
/// </summary>
public sealed class MacSafeExitService : ISafeExitService
{
    private readonly Action _exitAction;

    public MacSafeExitService(Action? exitAction = null)
    {
        _exitAction = exitAction ?? (() => Environment.Exit(0));
    }

    public void RequestExit() => _exitAction();
}
