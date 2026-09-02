using SecretBase.Core.Assistant;

namespace SecretBase.Infrastructure.Assistant;

internal static class LocalAiAvailabilityProbe
{
    public static async Task<bool> IsOllamaReachableAsync(
        HttpClient http,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var normalized = OllamaAssistantProvider.NormalizeBaseUrl(baseUrl);
        try
        {
            using var response = await http
                .GetAsync($"{normalized}/api/tags", cancellationToken)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }
}
