namespace SecretBase.Core.Security;

/// <summary>
/// Trust boundary categories. Used for documentation and future enforcement.
/// </summary>
public enum TrustBoundary
{
    /// <summary>App, Core, Infrastructure, Platform adapters.</summary>
    TrustedHost = 0,

    /// <summary>Built-in widgets shipped with Secret Base.</summary>
    BuiltInWidget = 1,

    /// <summary>WebView2 page content. Never trusted.</summary>
    WebContent = 2,

    /// <summary>Third-party plugins (future). Never trusted.</summary>
    Plugin = 3,

    /// <summary>AI providers / tools (future). Restricted by design.</summary>
    AiAgent = 4
}
