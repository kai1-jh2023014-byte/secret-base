namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Safe application lifecycle operations that return the user to the normal Windows desktop.
/// Closing Secret Base must never modify Explorer, Taskbar, or OS shell state.
/// </summary>
public interface ISafeExitService
{
    /// <summary>
    /// Requests a normal process shutdown. Windows itself is left untouched.
    /// </summary>
    void RequestExit();
}
