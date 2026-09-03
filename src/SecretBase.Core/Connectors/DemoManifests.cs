namespace SecretBase.Core.Connectors;

public static class DemoManifests
{
    public const string LocalTestJson =
        """
        {
          "schemaVersion": 1,
          "id": "demo.local-test",
          "name": "Local Test App",
          "version": "1.0",
          "description": "Loopback demo for state and stats reads.",
          "transport": "local",
          "baseUrl": "http://127.0.0.1:3847",
          "allowLoopback": true,
          "capabilities": [
            { "id": "state.read", "displayName": "Read state", "risk": "read", "endpointId": "state" },
            { "id": "stats.read", "displayName": "Read stats", "risk": "read", "endpointId": "stats" },
            { "id": "event.receive", "displayName": "Receive events", "risk": "read", "eventType": "task.completed" }
          ],
          "events": [
            { "type": "task.completed", "description": "A local task finished." }
          ],
          "authentication": { "kind": "none" },
          "endpoints": [
            { "id": "state", "method": "GET", "path": "/state", "timeoutMs": 2000, "maxResponseBytes": 4096 },
            { "id": "stats", "method": "GET", "path": "/stats", "timeoutMs": 2000, "maxResponseBytes": 4096 }
          ],
          "permissions": ["read", "backgroundEvent"]
        }
        """;

    public const string UserAppJson =
        """
        {
          "schemaVersion": 1,
          "id": "demo.user-app",
          "name": "Tetris AI",
          "version": "1.0",
          "description": "Personal Tetris AI — a user-built app connected without changing Secret Base.",
          "transport": "http",
          "baseUrl": "http://127.0.0.1:3848",
          "allowLoopback": true,
          "capabilities": [
            { "id": "state.read", "displayName": "Read state", "risk": "read", "endpointId": "state" },
            { "id": "stats.read", "displayName": "Read stats", "risk": "read", "endpointId": "stats" },
            { "id": "project.read", "displayName": "Latest project", "risk": "read", "endpointId": "latest" },
            { "id": "app.open", "displayName": "Start app", "risk": "execute" },
            { "id": "event.receive", "displayName": "Receive events", "risk": "read", "eventType": "game.finished" }
          ],
          "events": [
            { "type": "game.started", "description": "A game began." },
            { "type": "game.finished", "description": "A game ended." }
          ],
          "authentication": { "kind": "none" },
          "endpoints": [
            { "id": "state", "method": "GET", "path": "/state" },
            { "id": "stats", "method": "GET", "path": "/stats" },
            { "id": "latest", "method": "GET", "path": "/projects/latest" }
          ],
          "deepLink": { "scheme": "tetrisai", "host": "game", "pathPrefix": "/" },
          "permissions": ["read", "execute", "backgroundEvent"]
        }
        """;

    public const string GenericRestJson =
        """
        {
          "schemaVersion": 1,
          "id": "demo.generic-rest",
          "name": "Generic REST",
          "version": "1.0",
          "description": "HTTPS REST connector restricted to declared endpoints.",
          "transport": "https",
          "baseUrl": "https://api.example.invalid",
          "capabilities": [
            { "id": "project.read", "displayName": "Read project", "risk": "read", "endpointId": "project" },
            { "id": "data.create", "displayName": "Create record", "risk": "write", "endpointId": "create" },
            { "id": "data.delete", "displayName": "Delete record", "risk": "destructive", "endpointId": "delete" }
          ],
          "events": [],
          "authentication": { "kind": "apiKey", "headerName": "Authorization" },
          "endpoints": [
            {
              "id": "project",
              "method": "GET",
              "path": "/v1/project",
              "requestSchema": { "type": "object", "additionalProperties": false, "properties": {} }
            },
            {
              "id": "create",
              "method": "POST",
              "path": "/v1/items",
              "requestSchema": {
                "type": "object",
                "additionalProperties": false,
                "required": ["title"],
                "properties": { "title": { "type": "string", "maxLength": 80 } }
              }
            },
            {
              "id": "delete",
              "method": "DELETE",
              "path": "/v1/items/{id}",
              "requestSchema": {
                "type": "object",
                "additionalProperties": false,
                "required": ["id"],
                "properties": { "id": { "type": "string", "maxLength": 64 } }
              }
            }
          ],
          "permissions": ["read", "write", "destructive", "externalCommunication"]
        }
        """;

    public static IntegrationManifest LocalTest() => Parse(LocalTestJson);

    public static IntegrationManifest UserApp() => Parse(UserAppJson);

    public static IntegrationManifest GenericRest() => Parse(GenericRestJson);

    public static IntegrationManifest Calendar()
    {
        var manifest = new IntegrationManifest
        {
            Id = IntegrationIds.Calendar,
            Name = "Secret Base Calendar",
            Version = "1.0",
            Description = "Built-in calendar adapter over existing Calendar commands. No Google SDK.",
            Transport = IntegrationTransportKind.Local,
            Capabilities =
            [
                new IntegrationCapabilityDeclaration
                {
                    Id = IntegrationCapabilityIds.CalendarList,
                    DisplayName = "List local events",
                    Risk = CapabilityRiskKind.Read
                }
            ],
            Authentication = new IntegrationAuthenticationDeclaration { Kind = IntegrationAuthKind.None },
            RequestedPermissions = IntegrationPermissionKind.Read
        };
        if (!IntegrationManifestValidator.TryValidate(manifest, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return manifest;
    }

    public static bool TryRegisterKnown(
        IIntegrationRegistry registry,
        bool approveDemos = false)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (registry.Find(IntegrationIds.Calendar) is null)
        {
            registry.TryRegister(
                Calendar(),
                approved: true,
                IntegrationDiscoveryKind.KnownAppRegistration,
                out _);
        }

        if (approveDemos)
        {
            if (registry.Find(IntegrationIds.LocalTest) is null)
            {
                registry.TryRegister(
                    LocalTest(),
                    approved: true,
                    IntegrationDiscoveryKind.ExplicitRegistration,
                    out _);
            }
            else
            {
                registry.SetEnabled(IntegrationIds.LocalTest, true);
            }

            if (registry.Find(IntegrationIds.UserApp) is null)
            {
                registry.TryRegister(
                    UserApp(),
                    approved: true,
                    IntegrationDiscoveryKind.UserSelectedManifest,
                    out _);
            }
            else
            {
                registry.SetEnabled(IntegrationIds.UserApp, true);
            }
        }

        return true;
    }

    private static IntegrationManifest Parse(string json)
    {
        if (!IntegrationManifestParser.TryParse(json, out var manifest, out var error) || manifest is null)
        {
            throw new InvalidOperationException(error);
        }

        return manifest;
    }
}

/// <summary>
/// MCP-style tools must still declare a capability, pass permission + safety, and never see secrets.
/// v1.1 does not execute arbitrary MCP servers.
/// </summary>
public sealed class McpToolBinding
{
    public string ServerId { get; init; } = string.Empty;

    public string ToolName { get; init; } = string.Empty;

    public string CapabilityId { get; init; } = string.Empty;

    public CapabilityRiskKind Risk { get; init; } = CapabilityRiskKind.Read;
}

public interface IMcpBridge
{
    bool IsEnabled { get; }

    IReadOnlyList<McpToolBinding> ListBindings();
}

public sealed class DisabledMcpBridge : IMcpBridge
{
    public bool IsEnabled => false;

    public IReadOnlyList<McpToolBinding> ListBindings() => [];
}

public static class IntegrationPrivacy
{
    public const string Observed =
        "Declared integration names, capability ids, health, last-used time, and sanitized event summaries.";

    public const string Shared =
        "Only capabilities you grant. Reads stay in Secret Base until you remember them. Writes and launches confirm.";

    public const string Remembered =
        "Nothing from an integration is stored as Memory unless you explicitly save it.";

    public const string Permissions =
        "Per-integration Read, Write, Execute, External Communication, Destructive, and Background Event.";

    public static string Format(IntegrationHost? host)
    {
        var body = string.Join(
            Environment.NewLine,
            [
                "Integrations",
                Observed,
                string.Empty,
                "Shared",
                Shared,
                string.Empty,
                "Remembered",
                Remembered,
                string.Empty,
                "Permissions",
                Permissions
            ]);
        if (host is null)
        {
            return body;
        }

        return body
               + Environment.NewLine
               + Environment.NewLine
               + host.PermissionText();
    }
}
