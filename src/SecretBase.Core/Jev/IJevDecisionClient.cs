namespace SecretBase.Core.Jev;

/// <summary>HTTP boundary. Implementations must not launch processes or touch the filesystem.</summary>
public interface IJevDecisionClient
{
    Task<JevClientResult> DecideAsync(
        string apiKey,
        string requestJson,
        CancellationToken cancellationToken = default);
}
