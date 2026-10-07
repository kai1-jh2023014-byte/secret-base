namespace SecretBase.Core.Blocks;

/// <summary>
/// Suppresses duplicate launches of the same absolute target within a short window.
/// Guards against UI re-entrancy (PointerReleased + PointerCaptureLost) firing twice
/// for one user click — especially painful for .bat/.cmd which open new consoles.
/// </summary>
public sealed class TargetLaunchCoalescer
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(500);

    private readonly TimeSpan _window;
    private string? _lastTarget;
    private DateTimeOffset _lastAt;

    public TargetLaunchCoalescer(TimeSpan? window = null)
    {
        _window = window ?? DefaultWindow;
    }

    /// <summary>
    /// Returns true when this target should be launched now; false when it is a duplicate
    /// of a very recent launch of the same path.
    /// </summary>
    public bool TryAdmit(string target, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (_lastTarget is not null
            && string.Equals(_lastTarget, target, StringComparison.OrdinalIgnoreCase)
            && now - _lastAt < _window)
        {
            return false;
        }

        _lastTarget = target;
        _lastAt = now;
        return true;
    }
}
