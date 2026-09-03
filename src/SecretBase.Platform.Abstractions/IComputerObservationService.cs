using SecretBase.Core.Observation;

namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Optional computer observation. Implementations live in Platform.Windows / Mac.
/// Core never calls Win32.
/// </summary>
public interface IComputerObservationService : IDisposable
{
    bool IsSupported { get; }

    string CapabilityNote { get; }

    event EventHandler<ObservationEvent>? Observed;

    void Start();

    void Stop();
}

/// <summary>macOS / tests / unsupported hosts. Honest no-op — not a fake production observer.</summary>
public sealed class NullComputerObservationService : IComputerObservationService
{
    public bool IsSupported => false;

    public string CapabilityNote =>
        "Foreground observation is not available on this host. Secret Base still records its own activity.";

    public event EventHandler<ObservationEvent>? Observed
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
