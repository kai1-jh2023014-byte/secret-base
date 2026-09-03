namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Safe application lifecycle operations that end the Secret Base process only.
/// Closing Secret Base must never modify Explorer, Dock, Taskbar, Finder, or OS shell state.
/// </summary>
public interface ISafeExitService
{
    /// <summary>
    /// Requests a normal process shutdown. The host OS shell is left untouched.
    /// </summary>
    void RequestExit();
}
