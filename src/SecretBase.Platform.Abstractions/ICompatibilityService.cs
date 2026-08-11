namespace SecretBase.Platform.Abstractions;

/// <summary>
/// Reads host environment facts through the Platform layer.
/// </summary>
public interface ICompatibilityService
{
    CompatibilityInfo GetCurrent();
}
