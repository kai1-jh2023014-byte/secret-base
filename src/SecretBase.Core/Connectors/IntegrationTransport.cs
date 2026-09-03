namespace SecretBase.Core.Connectors;

public sealed record TransportRequest(
    string IntegrationId,
    string Method,
    Uri Url,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    TimeSpan Timeout,
    int MaxResponseBytes);

public sealed record TransportResponse(
    bool Succeeded,
    int StatusCode,
    string Body,
    string? ErrorCategory,
    string? ErrorMessage,
    bool TimedOut = false,
    bool Oversized = false,
    bool Redirected = false);

public interface IIntegrationTransport
{
    Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Loopback test transport. No sockets. Used by demo connectors and Core tests.</summary>
public sealed class LoopbackIntegrationTransport : IIntegrationTransport
{
    private readonly Dictionary<string, Func<TransportRequest, TransportResponse>> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    public TimeSpan? ForcedDelay { get; set; }

    public int? ForcedOversizeBytes { get; set; }

    public LoopbackIntegrationTransport()
    {
        Map("http://127.0.0.1:3847/state", _ => Ok("""{"status":"idle","score":0,"level":1}"""));
        Map("http://127.0.0.1:3847/stats", _ => Ok("""{"games":3,"best":1200,"wins":1}"""));
        Map("http://127.0.0.1:3848/state", _ => Ok("""{"status":"playing","lines":12,"score":840}"""));
        Map("http://127.0.0.1:3848/stats", _ => Ok("""{"gamesToday":2,"best":2048,"lastResult":"topped out"}"""));
        Map("http://127.0.0.1:3848/projects/latest", _ => Ok("""{"name":"Sprint study","updated":"today"}"""));
    }

    public void Map(string url, Func<TransportRequest, TransportResponse> handler) => _routes[url] = handler;

    public async Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ForcedDelay is { } delay)
        {
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new TransportResponse(false, 0, string.Empty, "timeout", "Request timed out.", TimedOut: true);
            }
        }

        if (request.Timeout < TimeSpan.FromMilliseconds(50))
        {
            return new TransportResponse(false, 0, string.Empty, "timeout", "Request timed out.", TimedOut: true);
        }

        if (!HttpEndpointPolicy.IsLoopbackHost(request.Url.Host))
        {
            return new TransportResponse(false, 0, string.Empty, "denied-host", "Host is not allowed.");
        }

        if (ForcedOversizeBytes is > 0)
        {
            return new TransportResponse(
                false,
                200,
                string.Empty,
                "oversized",
                "Response exceeded the size limit.",
                Oversized: true);
        }

        var key = request.Url.GetLeftPart(UriPartial.Path);
        if (!_routes.TryGetValue(key, out var handler)
            && !_routes.TryGetValue(request.Url.AbsoluteUri, out handler))
        {
            return new TransportResponse(false, 404, string.Empty, "not-found", "Endpoint is not declared.");
        }

        var response = handler(request);
        if (response.Body.Length > request.MaxResponseBytes)
        {
            return response with
            {
                Succeeded = false,
                Oversized = true,
                ErrorCategory = "oversized",
                ErrorMessage = "Response exceeded the size limit.",
                Body = string.Empty
            };
        }

        return response;
    }

    public static TransportResponse Ok(string body) => new(true, 200, body, null, null);
}

public sealed class IntegrationRateLimiter
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<DateTimeOffset>> _hits = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _max;
    private readonly TimeSpan _window;

    public IntegrationRateLimiter(int maxPerWindow = 30, TimeSpan? window = null)
    {
        _max = Math.Max(1, maxPerWindow);
        _window = window ?? TimeSpan.FromMinutes(1);
    }

    public bool TryAcquire(string integrationId, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_hits.TryGetValue(integrationId, out var list))
            {
                list = [];
                _hits[integrationId] = list;
            }

            list.RemoveAll(stamp => now - stamp > _window);
            if (list.Count >= _max)
            {
                return false;
            }

            list.Add(now);
            return true;
        }
    }
}

public sealed class IntegrationReadCache
{
    private readonly Dictionary<string, CacheEntry> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public (string? Value, IntegrationFreshness Freshness) Get(string key, DateTimeOffset now, TimeSpan ttl)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(key, out var entry))
            {
                return (null, IntegrationFreshness.Unavailable);
            }

            return now - entry.At <= ttl
                ? (entry.Value, IntegrationFreshness.Fresh)
                : (entry.Value, IntegrationFreshness.Stale);
        }
    }

    public void Set(string key, string value, DateTimeOffset now)
    {
        lock (_gate)
        {
            _items[key] = new CacheEntry(value, now);
        }
    }

    private sealed record CacheEntry(string Value, DateTimeOffset At);
}
