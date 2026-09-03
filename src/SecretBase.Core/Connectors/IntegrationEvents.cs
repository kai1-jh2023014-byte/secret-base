using System.Text.Json;
using SecretBase.Core.Activity;
using SecretBase.Core.Automation;
using SecretBase.Core.Calendar;

namespace SecretBase.Core.Connectors;

public sealed record ExternalIntegrationEvent
{
    public string Source { get; init; } = "connector";

    public string IntegrationId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public string SafePayload { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed record WebhookEnvelope
{
    public string IntegrationId { get; init; } = string.Empty;

    public string EventType { get; init; } = string.Empty;

    public string Timestamp { get; init; } = string.Empty;

    public string Nonce { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;

    public string PayloadJson { get; init; } = "{}";
}

public static class IntegrationEventNormalizer
{
    public const int MaxPayloadChars = 2_000;

    public static bool TryNormalize(
        IntegrationRegistration registration,
        string eventType,
        DateTimeOffset timestamp,
        string? payloadJson,
        string? correlationId,
        out ExternalIntegrationEvent? normalized,
        out string error)
    {
        normalized = null;
        error = string.Empty;
        if (!registration.Manifest.Events.Any(item =>
                item.Type.Equals(eventType, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Event type is not declared.";
            return false;
        }

        var raw = payloadJson ?? string.Empty;
        if (raw.Length > MaxPayloadChars)
        {
            error = "Event payload is too large.";
            return false;
        }

        var safe = IntegrationSecretSanitizer.Redact(raw);
        if (safe.Length > 280)
        {
            safe = safe[..280];
        }

        JsonElement payload = default;
        if (!string.IsNullOrWhiteSpace(payloadJson))
        {
            try
            {
                payload = JsonDocument.Parse(payloadJson).RootElement.Clone();
            }
            catch (JsonException)
            {
                error = "Event payload is not valid JSON.";
                return false;
            }

            payload = IntegrationSecretSanitizer.RedactJson(payload);
        }

        var summary = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("summary", out var s)
            ? s.GetString()
            : null;
        normalized = new ExternalIntegrationEvent
        {
            Source = registration.DisplayName,
            IntegrationId = registration.Id,
            EventType = eventType.Trim().ToLowerInvariant(),
            Timestamp = timestamp,
            SafePayload = string.IsNullOrWhiteSpace(summary) ? safe : IntegrationSecretSanitizer.Redact(summary),
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId
        };
        return true;
    }

    public static ActivityEvent ToActivity(ExternalIntegrationEvent evt, string? projectName = null) => new()
    {
        Kind = ActivityKind.IntegrationEvent,
        At = evt.Timestamp,
        Title = evt.Source + " · " + evt.EventType,
        Detail = evt.SafePayload,
        ProjectName = projectName ?? evt.Source,
        Source = "integration:" + evt.IntegrationId,
        CorrelationId = evt.CorrelationId,
        Confidence = 0.7
    };
}

public sealed class WebhookIngestor : IIntegrationEventSource
{
    public static readonly TimeSpan ReplayWindow = TimeSpan.FromMinutes(5);
    private readonly IIntegrationRegistry _registry;
    private readonly IIntegrationSecretResolver _secrets;
    private readonly IntegrationRateLimiter _rate;
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _nonces = new(StringComparer.Ordinal);

    public WebhookIngestor(
        IIntegrationRegistry registry,
        IIntegrationSecretResolver? secrets = null,
        IntegrationRateLimiter? rate = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _secrets = secrets ?? new NullIntegrationSecretResolver();
        _rate = rate ?? new IntegrationRateLimiter(20, TimeSpan.FromMinutes(1));
    }

    public bool TryIngest(
        WebhookEnvelope envelope,
        DateTimeOffset now,
        out ExternalIntegrationEvent? normalized,
        out string error)
    {
        normalized = null;
        error = string.Empty;
        if (envelope.PayloadJson.Length > IntegrationEventNormalizer.MaxPayloadChars)
        {
            error = "Webhook payload is too large.";
            return false;
        }

        var registration = _registry.Find(envelope.IntegrationId);
        if (registration is null || !registration.Enabled || !registration.Approved)
        {
            error = "Unknown or disabled integration.";
            return false;
        }

        if (!IntegrationPermissionGate.AllowsBackgroundEvents(registration.Permissions))
        {
            error = "Background events are not permitted.";
            return false;
        }

        if (!_rate.TryAcquire(registration.Id, now))
        {
            error = "Rate limited.";
            _registry.Touch(registration.Id, now, IntegrationHealthStatus.RateLimited, "webhook");
            return false;
        }

        if (!long.TryParse(envelope.Timestamp, out var unix)
            || Math.Abs((now - DateTimeOffset.FromUnixTimeSeconds(unix)).TotalSeconds) > ReplayWindow.TotalSeconds)
        {
            error = "Webhook timestamp is invalid.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Nonce) || envelope.Nonce.Length > 64)
        {
            error = "Webhook nonce is invalid.";
            return false;
        }

        lock (_gate)
        {
            Prune(now);
            if (_nonces.ContainsKey(envelope.Nonce))
            {
                error = "Replay detected.";
                return false;
            }
        }

        var reference = registration.CredentialReference
                        ?? CredentialReference.For(registration.Id, "webhook");
        if (!_secrets.TryResolveHeader(reference, "X-Secret-Base-Signature", out var secret)
            || string.IsNullOrWhiteSpace(secret))
        {
            error = "Webhook is not authenticated.";
            return false;
        }

        var expected = IntegrationHmac.Sign(secret, envelope.Timestamp, envelope.Nonce, envelope.PayloadJson);
        if (!IntegrationHmac.EqualsFixed(expected, envelope.Signature ?? string.Empty))
        {
            error = "Webhook signature is invalid.";
            return false;
        }

        lock (_gate)
        {
            _nonces[envelope.Nonce] = now;
        }

        if (!IntegrationEventNormalizer.TryNormalize(
                registration,
                envelope.EventType,
                DateTimeOffset.FromUnixTimeSeconds(unix),
                envelope.PayloadJson,
                envelope.Nonce,
                out normalized,
                out error))
        {
            return false;
        }

        return true;
    }

    private void Prune(DateTimeOffset now)
    {
        var stale = _nonces.Where(pair => now - pair.Value > ReplayWindow).Select(pair => pair.Key).ToList();
        foreach (var key in stale)
        {
            _nonces.Remove(key);
        }
    }
}

public static class IntegrationAutomation
{
    public static AutomationSuggestion? Suggest(
        ExternalIntegrationEvent evt,
        AutomationRule? rule,
        DateTimeOffset now)
    {
        if (rule is null || !rule.Enabled)
        {
            return null;
        }

        if (rule.Intervention is InterventionMode.Silent or InterventionMode.Passive)
        {
            return null;
        }

        var confirm = rule.RequiresConfirmation || rule.Intervention is InterventionMode.Confirm or InterventionMode.Urgent;
        return new AutomationSuggestion
        {
            Intent = rule.RequiredIntent ?? SecretBase.Core.Intent.DetectedIntentKind.ContinueProject,
            Title = rule.Name,
            Detail = evt.SafePayload,
            Confidence = 0.62,
            RequiresConfirmation = confirm,
            Safety = confirm
                ? AutomationSafetyLevel.ConfirmationRequired
                : AutomationSafetyLevel.SafeAuto,
            ProjectName = evt.Source,
            Evidence = ["integration-event", evt.IntegrationId, evt.EventType, "rule:" + rule.Id]
        };
    }
}

/// <summary>Wraps the existing local calendar Command surface as a connector — no Google SDK in Core.</summary>
public sealed class BuiltInCalendarConnector
{
    private readonly CalendarCommandService? _calendar;

    public BuiltInCalendarConnector(CalendarCommandService? calendar = null)
    {
        _calendar = calendar;
    }

    public ConnectorOutcome Read(IntegrationRegistration registration)
    {
        if (_calendar is null)
        {
            return ConnectorOutcome.Unavailable(registration, "Calendar is not available.");
        }

        var events = _calendar.ListLocalEvents();
        var lines = events.Take(8).Select(item => item.Title).ToList();
        var body = lines.Count == 0 ? "No local calendar events." : string.Join(Environment.NewLine, lines);
        return ConnectorOutcome.Ok(
            registration,
            IntegrationActionLane.Read,
            IntegrationPromptGuard.Wrap(registration.Id, body),
            body);
    }
}
