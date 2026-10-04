namespace SecretBase.Core.Jev;

/// <summary>
/// Asks Jev for a typed decision, validates it, and applies <see cref="JevSafetyGate"/>.
/// Does not call tools.
/// </summary>
public interface IJevDecisionService
{
    bool IsConfigured { get; }

    Task<JevDecisionOutcome> DecideAsync(
        JevObservation observation,
        CancellationToken cancellationToken = default);

    Task<JevConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class JevDecisionService : IJevDecisionService
{
    private readonly IJevDecisionClient _client;
    private readonly Func<string?> _getApiKey;

    public JevDecisionService(IJevDecisionClient client, Func<string?> getApiKey)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _getApiKey = getApiKey ?? throw new ArgumentNullException(nameof(getApiKey));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_getApiKey());

    public async Task<JevDecisionOutcome> DecideAsync(
        JevObservation observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var key = _getApiKey()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Fallback(observation, JevUserMessages.NotConfigured);
        }

        var request = JevDecisionContract.BuildDecisionRequest(JevObservationFormatter.Format(observation));
        JevClientResult result;
        try
        {
            result = await _client.DecideAsync(key, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Fallback(observation, JevUserMessages.Unavailable);
        }

        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Body))
        {
            return Fallback(observation, result.ErrorMessage ?? JevUserMessages.Unavailable);
        }

        var decision = JevDecisionParser.Parse(result.Body);
        return new JevDecisionOutcome
        {
            Decision = decision,
            Verdict = JevSafetyGate.Evaluate(decision),
            UsedFallback = false,
            Error = decision.IsValid ? null : decision.InvalidReason
        };
    }

    public async Task<JevConnectionTest> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var key = _getApiKey()?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return JevConnectionTest.NotConfigured();
        }

        var request = JevDecisionContract.BuildConnectionCheckRequest();
        JevClientResult result;
        try
        {
            result = await _client.DecideAsync(key, request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return JevConnectionTest.Fail(JevUserMessages.Unavailable);
        }

        if (!result.Succeeded)
        {
            return JevConnectionTest.Fail(result.ErrorMessage ?? JevUserMessages.Unavailable);
        }

        if (!JevDecisionParser.IsConnectionCheck(result.Body))
        {
            return JevConnectionTest.Fail(JevUserMessages.InvalidResponse);
        }

        return JevConnectionTest.Ok(JevDecisionParser.ReadModel(result.Body));
    }

    private static JevDecisionOutcome Fallback(JevObservation observation, string error)
    {
        var decision = JevDecisionFallback.FromObservation(observation);
        return new JevDecisionOutcome
        {
            Decision = decision,
            Verdict = JevSafetyGate.Evaluate(decision),
            UsedFallback = true,
            Error = error
        };
    }
}
