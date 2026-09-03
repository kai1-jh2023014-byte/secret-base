using System.Diagnostics;
using System.Text.Json;
using SecretBase.Core.Activity;
using SecretBase.Core.Apps;
using SecretBase.Core.Automation;
using SecretBase.Core.Calendar;
using SecretBase.Core.Memory;

namespace SecretBase.Core.Connectors;

public sealed class ConnectorInvocation
{
    public required string IntegrationId { get; init; }

    public required string CapabilityId { get; init; }

    public string ArgumentsJson { get; init; } = "{}";

    public bool UserConfirmed { get; init; }

    public bool RememberResult { get; init; }
}

public sealed class ConnectorOutcome
{
    public bool Succeeded { get; init; }

    public bool NeedsConfirmation { get; init; }

    public IntegrationActionLane Lane { get; init; }

    public string Message { get; init; } = string.Empty;

    public string SanitizedPayload { get; init; } = string.Empty;

    public IntegrationFreshness Freshness { get; init; } = IntegrationFreshness.Fresh;

    public IntegrationHealthStatus Health { get; init; } = IntegrationHealthStatus.Connected;

    public bool ShouldLaunch { get; init; }

    public string? LaunchTarget { get; init; }

    public bool LaunchIsExternalLink { get; init; }

    public string? ErrorCategory { get; init; }

    public string? ActivityTitle { get; init; }

    public string IntegrationId { get; init; } = string.Empty;

    public static ConnectorOutcome Fail(
        IntegrationRegistration? registration,
        string message,
        string category,
        IntegrationHealthStatus health = IntegrationHealthStatus.Error,
        IntegrationActionLane lane = IntegrationActionLane.Forbidden) =>
        new()
        {
            Succeeded = false,
            Message = message,
            ErrorCategory = category,
            Health = health,
            Lane = lane,
            IntegrationId = registration?.Id ?? string.Empty
        };

    public static ConnectorOutcome Unavailable(IntegrationRegistration registration, string message) =>
        Fail(registration, message, "unavailable", IntegrationHealthStatus.Unavailable, IntegrationActionLane.Read);

    public static ConnectorOutcome Ok(
        IntegrationRegistration registration,
        IntegrationActionLane lane,
        string message,
        string payload,
        IntegrationFreshness freshness = IntegrationFreshness.Fresh,
        bool shouldLaunch = false,
        string? launchTarget = null,
        bool launchIsExternalLink = false) =>
        new()
        {
            Succeeded = true,
            Lane = lane,
            Message = message,
            SanitizedPayload = payload,
            Freshness = freshness,
            Health = IntegrationHealthStatus.Connected,
            ShouldLaunch = shouldLaunch,
            LaunchTarget = launchTarget,
            LaunchIsExternalLink = launchIsExternalLink,
            IntegrationId = registration.Id,
            ActivityTitle = registration.DisplayName + " · " + lane
        };
}

/// <summary>
/// Integration Host: Registry → Capability → Permission → Safety → Transport → Normalized result.
/// One connector failure never throws out of the host.
/// </summary>
public sealed class IntegrationHost
{
    public static readonly TimeSpan ReadTtl = TimeSpan.FromSeconds(30);

    private readonly IIntegrationRegistry _registry;
    private readonly IIntegrationTransport _transport;
    private readonly IIntegrationSecretResolver _secrets;
    private readonly IntegrationRateLimiter _rate = new();
    private readonly IntegrationReadCache _cache = new();
    private readonly WebhookIngestor _webhooks;
    private readonly BuiltInCalendarConnector _calendar;
    private readonly AppCommandService? _apps;
    private readonly IActivityLog? _activity;
    private readonly IMemoryStore? _memory;
    private readonly IAutomationRuleStore? _rules;
    private readonly List<IntegrationLogEntry> _logs = [];
    private readonly object _logGate = new();

    public IntegrationHost(
        IIntegrationRegistry registry,
        IIntegrationTransport? transport = null,
        IIntegrationSecretResolver? secrets = null,
        CalendarCommandService? calendar = null,
        AppCommandService? apps = null,
        IActivityLog? activity = null,
        IMemoryStore? memory = null,
        IAutomationRuleStore? rules = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _transport = transport ?? new LoopbackIntegrationTransport();
        _secrets = secrets ?? new NullIntegrationSecretResolver();
        _webhooks = new WebhookIngestor(_registry, _secrets);
        _calendar = new BuiltInCalendarConnector(calendar);
        _apps = apps;
        _activity = activity;
        _memory = memory;
        _rules = rules;
    }

    public IIntegrationRegistry Registry => _registry;

    public WebhookIngestor Webhooks => _webhooks;

    public IReadOnlyList<IntegrationLogEntry> Logs
    {
        get
        {
            lock (_logGate)
            {
                return _logs.ToList();
            }
        }
    }

    public IReadOnlyList<string> ContextFacts(int take = 8) =>
        _registry.List()
            .Where(item => item.Approved)
            .Take(take)
            .Select(item =>
                $"{item.DisplayName} ({item.Manifest.Transport}, {(item.Enabled ? item.Health : IntegrationHealthStatus.Disabled)})")
            .ToList();

    public string CatalogText()
    {
        var items = _registry.List();
        if (items.Count == 0)
        {
            return "No integrations registered. Add a secretbase.integration.json and approve it.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            items.Select(FormatRegistration));
    }

    public string PermissionText(string? id = null)
    {
        var items = string.IsNullOrWhiteSpace(id)
            ? _registry.List()
            : _registry.Find(id) is { } one ? [one] : [];
        if (items.Count == 0)
        {
            return "No integrations.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            items.Select(item =>
            {
                var rows = IntegrationPermissionGate.Matrix(item.Manifest, item.Permissions);
                return item.DisplayName
                       + Environment.NewLine
                       + string.Join(
                           Environment.NewLine,
                           rows.Select(row => "  " + (row.Granted ? "✓" : "✗") + "  " + row.Label));
            }));
    }

    public string DescribeForAi()
    {
        var items = _registry.List().Where(item => item.Approved && item.Enabled).ToList();
        if (items.Count == 0)
        {
            return "No approved integrations.";
        }

        var lines = new List<string> { "Available integrations (named capabilities only — never invent URLs):" };
        foreach (var item in items)
        {
            lines.Add(item.DisplayName + " id=" + item.Id + " health=" + item.Health);
            foreach (var capability in item.Manifest.Capabilities)
            {
                var lane = IntegrationSafety.Classify(
                    capability,
                    item.Manifest.Endpoints.FirstOrDefault(e => e.Id == capability.EndpointId));
                lines.Add(
                    "  - "
                    + capability.Id
                    + " ["
                    + lane
                    + "]"
                    + (IntegrationSafety.RequiresConfirmation(lane) ? " (confirmation)" : string.Empty));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public IReadOnlyList<SecretBase.Core.Search.SearchHit> SearchHits(string query)
    {
        query ??= string.Empty;
        var hits = new List<SecretBase.Core.Search.SearchHit>();
        foreach (var item in _registry.List())
        {
            var blob = item.DisplayName + " " + item.Manifest.Description + " " + item.Id;
            if (query.Length == 0 || blob.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                hits.Add(new SecretBase.Core.Search.SearchHit(
                    "integration",
                    item.DisplayName,
                    item.Manifest.Transport + " · " + item.Health,
                    0.9,
                    item.LastUsedAt,
                    item.Id));
            }

            foreach (var capability in item.Manifest.Capabilities)
            {
                if (capability.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || (capability.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    hits.Add(new SecretBase.Core.Search.SearchHit(
                        "capability",
                        capability.DisplayName ?? capability.Id,
                        item.DisplayName + " · " + capability.Id,
                        0.8,
                        Source: item.Id));
                }
            }
        }

        return hits;
    }

    public async Task<ConnectorOutcome> InvokeAsync(
        ConnectorInvocation invocation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            var outcome = await InvokeCoreAsync(invocation, now, cancellationToken).ConfigureAwait(false);
            Log(invocation.IntegrationId, invocation.CapabilityId, now, outcome, Stopwatch.GetElapsedTime(started));
            return outcome;
        }
        catch (Exception ex)
        {
            var failed = ConnectorOutcome.Fail(
                _registry.Find(invocation.IntegrationId),
                "Integration failed in isolation.",
                "isolated-failure",
                IntegrationHealthStatus.Error);
            Log(invocation.IntegrationId, invocation.CapabilityId, now, failed, Stopwatch.GetElapsedTime(started));
            _ = ex;
            return failed;
        }
    }

    public AutomationSuggestion? IngestEvent(
        ExternalIntegrationEvent evt,
        DateTimeOffset now,
        string? projectName = null)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var activity = IntegrationEventNormalizer.ToActivity(evt, projectName);
        _activity?.Record(activity);
        var rule = _rules is null
            ? null
            : AutomationScheduler.MatchIntegration(_rules.List(), evt.IntegrationId, evt.EventType, now);
        if (rule is not null)
        {
            rule.LastRun = now;
            rule.LastResult = evt.EventType;
            _rules!.Save(rule);
        }

        return IntegrationAutomation.Suggest(evt, rule, now);
    }

    public bool TryIngestWebhook(
        WebhookEnvelope envelope,
        DateTimeOffset now,
        out ExternalIntegrationEvent? normalized,
        out AutomationSuggestion? suggestion,
        out string error)
    {
        suggestion = null;
        if (!_webhooks.TryIngest(envelope, now, out normalized, out error) || normalized is null)
        {
            return false;
        }

        suggestion = IngestEvent(normalized, now);
        return true;
    }

    private async Task<ConnectorOutcome> InvokeCoreAsync(
        ConnectorInvocation invocation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var registration = _registry.Find(invocation.IntegrationId);
        if (registration is null)
        {
            return ConnectorOutcome.Fail(null, "Unknown integration.", "unknown");
        }

        if (!registration.Approved)
        {
            return ConnectorOutcome.Fail(registration, "Integration is not approved.", "permission", IntegrationHealthStatus.PermissionDenied);
        }

        if (!registration.Enabled)
        {
            return ConnectorOutcome.Fail(registration, "Integration is disabled.", "disabled", IntegrationHealthStatus.Disabled);
        }

        var capability = registration.Manifest.Capabilities.FirstOrDefault(item =>
            item.Id.Equals(invocation.CapabilityId, StringComparison.OrdinalIgnoreCase));
        if (capability is null)
        {
            return ConnectorOutcome.Fail(registration, "Capability is not declared.", "capability", IntegrationHealthStatus.PermissionDenied);
        }

        JsonElement arguments;
        try
        {
            using var owned = JsonDocument.Parse(string.IsNullOrWhiteSpace(invocation.ArgumentsJson) ? "{}" : invocation.ArgumentsJson);
            arguments = owned.RootElement.Clone();
        }
        catch (JsonException)
        {
            return ConnectorOutcome.Fail(registration, "Arguments are not valid JSON.", "schema");
        }
        if (HttpEndpointPolicy.HasForbiddenCallerOverride(arguments))
        {
            return ConnectorOutcome.Fail(registration, "Arbitrary URL or HTTP fields are not allowed.", "forbidden");
        }

        var endpoint = string.IsNullOrWhiteSpace(capability.EndpointId)
            ? null
            : registration.Manifest.Endpoints.FirstOrDefault(item =>
                item.Id.Equals(capability.EndpointId, StringComparison.OrdinalIgnoreCase));
        var lane = IntegrationSafety.AfterLearning(IntegrationSafety.Classify(capability, endpoint));
        if (IntegrationSafety.IsForbidden(lane))
        {
            return ConnectorOutcome.Fail(registration, "This capability is forbidden.", "forbidden");
        }

        if (!IntegrationPermissionGate.Allows(registration.Permissions, capability.Risk)
            && !(string.Equals(capability.Id, IntegrationCapabilityIds.EventReceive, StringComparison.OrdinalIgnoreCase)
                 && IntegrationPermissionGate.AllowsBackgroundEvents(registration.Permissions)))
        {
            _registry.Touch(registration.Id, now, IntegrationHealthStatus.PermissionDenied);
            return ConnectorOutcome.Fail(
                registration,
                "Permission denied.",
                "permission",
                IntegrationHealthStatus.PermissionDenied,
                lane);
        }

        if (IntegrationSafety.RequiresConfirmation(lane) && !invocation.UserConfirmed)
        {
            return new ConnectorOutcome
            {
                Succeeded = false,
                NeedsConfirmation = true,
                Lane = lane,
                Message = "Confirm " + capability.DisplayName + " on " + registration.DisplayName + ".",
                Health = IntegrationHealthStatus.Connected,
                IntegrationId = registration.Id
            };
        }

        if (endpoint?.RequestSchema is { ValueKind: JsonValueKind.Object } requestSchema
            && !JsonSchemaLite.TryValidate(requestSchema, arguments, out var schemaError))
        {
            return ConnectorOutcome.Fail(registration, schemaError, "schema", IntegrationHealthStatus.Error, lane);
        }

        if (string.Equals(registration.Id, IntegrationIds.Calendar, StringComparison.OrdinalIgnoreCase)
            && string.Equals(capability.Id, IntegrationCapabilityIds.CalendarList, StringComparison.OrdinalIgnoreCase))
        {
            var calendar = _calendar.Read(registration);
            RememberIfRequested(invocation, registration, calendar.SanitizedPayload, now);
            _registry.Touch(registration.Id, now, calendar.Health);
            return calendar;
        }

        if (string.Equals(capability.Id, IntegrationCapabilityIds.AppOpen, StringComparison.OrdinalIgnoreCase))
        {
            return OpenApp(registration, invocation.UserConfirmed);
        }

        if (!_rate.TryAcquire(registration.Id, now))
        {
            _registry.Touch(registration.Id, now, IntegrationHealthStatus.RateLimited);
            return ConnectorOutcome.Fail(
                registration,
                "Rate limited.",
                "rate-limit",
                IntegrationHealthStatus.RateLimited,
                lane);
        }

        if (lane == IntegrationActionLane.Read)
        {
            var cacheKey = registration.Id + ":" + capability.Id;
            var cached = _cache.Get(cacheKey, now, ReadTtl);
            if (cached.Freshness == IntegrationFreshness.Fresh && cached.Value is not null)
            {
                return ConnectorOutcome.Ok(
                    registration,
                    lane,
                    IntegrationPromptGuard.Wrap(registration.Id, cached.Value),
                    cached.Value);
            }
        }

        if (endpoint is null)
        {
            return ConnectorOutcome.Fail(registration, "Capability has no endpoint.", "capability", IntegrationHealthStatus.Error, lane);
        }

        if (!HttpEndpointPolicy.TryCreateRequest(registration.Manifest, endpoint, arguments, out var request, out var httpError)
            || request is null)
        {
            return ConnectorOutcome.Fail(registration, httpError, "http-policy", IntegrationHealthStatus.Error, lane);
        }

        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase);
        if (registration.Manifest.Authentication.Kind != IntegrationAuthKind.None)
        {
            if (string.IsNullOrWhiteSpace(registration.CredentialReference)
                || !_secrets.TryResolveHeader(
                    registration.CredentialReference,
                    registration.Manifest.Authentication.HeaderName,
                    out var headerValue)
                || string.IsNullOrWhiteSpace(headerValue))
            {
                _registry.Touch(registration.Id, now, IntegrationHealthStatus.AuthenticationRequired);
                return ConnectorOutcome.Fail(
                    registration,
                    "Authentication is required.",
                    "auth",
                    IntegrationHealthStatus.AuthenticationRequired,
                    lane);
            }

            headers[registration.Manifest.Authentication.HeaderName ?? "Authorization"] = headerValue;
        }

        TransportResponse response;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(request.Timeout);
            response = await _transport.SendAsync(request with { Headers = headers }, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _registry.Touch(registration.Id, now, IntegrationHealthStatus.Unavailable, "timeout");
            return ConnectorOutcome.Fail(
                registration,
                "Request timed out.",
                "timeout",
                IntegrationHealthStatus.Unavailable,
                lane);
        }

        if (response.Redirected)
        {
            return ConnectorOutcome.Fail(registration, "Redirects are not followed.", "redirect", IntegrationHealthStatus.Error, lane);
        }

        if (response.TimedOut)
        {
            _registry.Touch(registration.Id, now, IntegrationHealthStatus.Unavailable, "timeout");
            return ConnectorOutcome.Fail(registration, "Request timed out.", "timeout", IntegrationHealthStatus.Unavailable, lane);
        }

        if (response.Oversized)
        {
            return ConnectorOutcome.Fail(registration, "Response exceeded the size limit.", "oversized", IntegrationHealthStatus.Error, lane);
        }

        if (!response.Succeeded)
        {
            var health = response.ErrorCategory == "denied-host"
                ? IntegrationHealthStatus.Error
                : IntegrationHealthStatus.Unavailable;
            _registry.Touch(registration.Id, now, health, response.ErrorCategory);
            return ConnectorOutcome.Fail(
                registration,
                response.ErrorMessage ?? "Connector unavailable.",
                response.ErrorCategory ?? "unavailable",
                health,
                lane);
        }

        var sanitized = IntegrationSecretSanitizer.Redact(response.Body);
        if (endpoint.ResponseSchema is { ValueKind: JsonValueKind.Object } responseSchema)
        {
            try
            {
                using var parsed = JsonDocument.Parse(string.IsNullOrWhiteSpace(sanitized) ? "{}" : sanitized);
                if (!JsonSchemaLite.TryValidate(responseSchema, parsed.RootElement, out var responseSchemaError))
                {
                    return ConnectorOutcome.Fail(registration, responseSchemaError, "schema", IntegrationHealthStatus.Error, lane);
                }
            }
            catch (JsonException)
            {
                return ConnectorOutcome.Fail(registration, "Response is not valid JSON.", "schema", IntegrationHealthStatus.Error, lane);
            }
        }

        if (lane == IntegrationActionLane.Read)
        {
            _cache.Set(registration.Id + ":" + capability.Id, sanitized, now);
        }

        RememberIfRequested(invocation, registration, sanitized, now);
        _registry.Touch(registration.Id, now, IntegrationHealthStatus.Connected);
        var wrapped = IntegrationPromptGuard.Wrap(registration.Id, sanitized);
        var freshnessNote = string.Empty;
        return ConnectorOutcome.Ok(registration, lane, wrapped + freshnessNote, sanitized);
    }

    private ConnectorOutcome OpenApp(IntegrationRegistration registration, bool confirmed)
    {
        if (!confirmed)
        {
            return new ConnectorOutcome
            {
                Succeeded = false,
                NeedsConfirmation = true,
                Lane = IntegrationActionLane.Confirm,
                Message = "Open " + registration.DisplayName + "?",
                IntegrationId = registration.Id
            };
        }

        if (_apps is null || string.IsNullOrWhiteSpace(registration.Manifest.LinkedAppId))
        {
            return ConnectorOutcome.Fail(
                registration,
                "Register this app in My Apps, then link it on the manifest. Raw deep links are not executed.",
                "launch",
                IntegrationHealthStatus.Error,
                IntegrationActionLane.Confirm);
        }

        var result = _apps.Execute(AppCommand.OpenApp(registration.Manifest.LinkedAppId));
        if (!result.Succeeded)
        {
            return ConnectorOutcome.Fail(
                registration,
                result.ErrorMessage ?? "App is not on the allowlist.",
                "launch",
                IntegrationHealthStatus.Error,
                IntegrationActionLane.Confirm);
        }

        return ConnectorOutcome.Ok(
            registration,
            IntegrationActionLane.Confirm,
            "Host may launch the registered app " + (result.App?.Name ?? registration.DisplayName) + ".",
            result.App?.Name ?? registration.DisplayName,
            shouldLaunch: result.ShouldLaunch,
            launchTarget: result.LaunchTarget,
            launchIsExternalLink: result.LaunchIsExternalLink);
    }

    private void RememberIfRequested(
        ConnectorInvocation invocation,
        IntegrationRegistration registration,
        string payload,
        DateTimeOffset now)
    {
        if (!invocation.RememberResult || _memory is null)
        {
            return;
        }

        if (IntegrationPromptGuard.LooksLikeInjection(payload) || CredentialReference.LooksLikeSecret(payload))
        {
            return;
        }

        try
        {
            _memory.Remember(new MemoryEntry
            {
                Scope = MemoryScope.Project,
                Key = "integration:" + registration.Id,
                Summary = registration.DisplayName + " snapshot",
                Detail = payload.Length > 160 ? payload[..160] : payload,
                ProjectName = registration.DisplayName,
                Source = "integration-user",
                Confidence = 0.4,
                Importance = MemoryImportance.Low,
                CreatedAt = now,
                LastAccessedAt = now,
                ExpiresAt = MemoryPolicy.DefaultExpiry(MemoryScope.Session, now)
            });
        }
        catch (InvalidOperationException)
        {
            // MemoryPolicy refuses secrets/paths.
        }
    }

    private void Log(
        string integrationId,
        string operation,
        DateTimeOffset now,
        ConnectorOutcome outcome,
        TimeSpan duration)
    {
        var entry = new IntegrationLogEntry
        {
            IntegrationId = integrationId,
            Operation = operation,
            Timestamp = now,
            Status = outcome.Succeeded ? "ok" : (outcome.NeedsConfirmation ? "confirm" : "error"),
            DurationMs = (int)duration.TotalMilliseconds,
            ErrorCategory = outcome.ErrorCategory
        };
        lock (_logGate)
        {
            _logs.Add(entry);
            if (_logs.Count > 200)
            {
                _logs.RemoveRange(0, _logs.Count - 200);
            }
        }
    }

    public static string FormatRegistration(IntegrationRegistration item) =>
        item.DisplayName
        + Environment.NewLine
        + $"Status {(item.Enabled ? item.Health : IntegrationHealthStatus.Disabled)} · v{item.Manifest.Version} · {item.Manifest.Transport}"
        + Environment.NewLine
        + "Capabilities " + string.Join(", ", item.Manifest.Capabilities.Select(c => c.Id))
        + Environment.NewLine
        + "Auth " + item.Manifest.Authentication.Kind
        + (string.IsNullOrWhiteSpace(item.CredentialReference) ? " · no credential" : " · credential reference only")
        + Environment.NewLine
        + "Last used " + (item.LastUsedAt?.ToString("g") ?? "never")
        + (item.Enabled ? " · enabled" : " · disabled");

}

public sealed class IntegrationLogEntry
{
    public string IntegrationId { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; }

    public string Status { get; init; } = string.Empty;

    public int DurationMs { get; init; }

    public string? ErrorCategory { get; init; }
}

public static class IntegrationExport
{
    public static string Export(IIntegrationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        var bundle = new IntegrationExportBundle
        {
            Schema = 1,
            Items = registry.List().Select(item => new IntegrationExportItem
            {
                ManifestJson = IntegrationManifestParser.Serialize(item.Manifest),
                Enabled = item.Enabled,
                Approved = item.Approved,
                Permissions = IntegrationManifestParser.FormatPermissions(item.Permissions),
                Settings = item.Settings
            }).ToList()
        };
        return JsonSerializer.Serialize(bundle, IntegrationManifestParser.CreateOptions());
    }

    public static int Import(IIntegrationRegistry registry, string json, out string error)
    {
        error = string.Empty;
        try
        {
            var bundle = JsonSerializer.Deserialize<IntegrationExportBundle>(json, IntegrationManifestParser.CreateOptions());
            if (bundle?.Items is null)
            {
                error = "Export bundle is empty.";
                return 0;
            }

            var count = 0;
            foreach (var item in bundle.Items)
            {
                if (!IntegrationManifestParser.TryParse(item.ManifestJson, out var manifest, out _) || manifest is null)
                {
                    continue;
                }

                if (registry.Find(manifest.Id) is not null)
                {
                    continue;
                }

                if (!registry.TryRegister(manifest, item.Approved, IntegrationDiscoveryKind.UserSelectedManifest, out _))
                {
                    continue;
                }

                registry.SetEnabled(manifest.Id, item.Enabled && item.Approved);
                registry.SetPermissions(manifest.Id, IntegrationManifestParser.ParsePermissions(item.Permissions));
                count++;
            }

            return count;
        }
        catch (JsonException)
        {
            error = "Export bundle is invalid.";
            return 0;
        }
    }

    private sealed class IntegrationExportBundle
    {
        public int Schema { get; set; } = 1;

        public List<IntegrationExportItem> Items { get; set; } = [];
    }

    private sealed class IntegrationExportItem
    {
        public string ManifestJson { get; set; } = "{}";

        public bool Enabled { get; set; }

        public bool Approved { get; set; }

        public List<string> Permissions { get; set; } = [];

        public Dictionary<string, string> Settings { get; set; } = new();
    }
}
