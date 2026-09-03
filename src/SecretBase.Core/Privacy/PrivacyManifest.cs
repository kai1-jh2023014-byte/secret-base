using SecretBase.Core.Connectors;

namespace SecretBase.Core.Privacy;

/// <summary>User-facing catalog of what Secret Base observes, remembers, and never collects.</summary>
public static class PrivacyManifest
{
    public const string Observed =
        "Foreground process name, idle vs active, Secret Base clicks, registered project/app names, "
        + "calendar titles, todo titles, workspace titles, session summaries.";

    public const string Remembered =
        "Scoped memory you (or Safe Auto) write: session, project, workflow, decision, preference, idea, note. "
        + "Importance, source, expiry. Ranked recall — never a full dump.";

    public const string Allowed =
        "Prepare a workspace, start a local focus timer, remember a short fact, suggest cleanup candidates, "
        + "show a quiet continuation card.";

    public const string RequiresConfirmation =
        "Open a registered project or app, continue a workspace into an app, calendar writes, music play, "
        + "unregister / return a Block item.";

    public const string NeverCollected =
        "Keystrokes, clipboard, passwords, API keys, tokens, browser page contents, microphone, camera, "
        + "screenshots, arbitrary document bodies, unrestricted filesystem paths.";

    public const string NeverAutomatic =
        "Arbitrary shell, PowerShell, arbitrary executables, OS file delete, silent app launch, "
        + "learning that turns Confirmation into Auto Action, arbitrary HTTP, silent integration writes.";

    public const string Integrations =
        "Registered connectors only. Named capabilities. External data is untrusted. Secrets never reach AI.";

    public static string Format() =>
        string.Join(
            Environment.NewLine,
            [
                "What does Secret Base observe?",
                Observed,
                string.Empty,
                "What does it remember?",
                Remembered,
                string.Empty,
                "What can it do automatically?",
                Allowed,
                string.Empty,
                "What requires confirmation?",
                RequiresConfirmation,
                string.Empty,
                "Never collected",
                NeverCollected,
                string.Empty,
                "Never automatic",
                NeverAutomatic,
                string.Empty,
                "Integrations",
                Integrations,
                string.Empty,
                "Observed (integrations)",
                IntegrationPrivacy.Observed,
                string.Empty,
                "Shared",
                IntegrationPrivacy.Shared,
                string.Empty,
                "Remembered (integrations)",
                IntegrationPrivacy.Remembered,
                string.Empty,
                "Permissions",
                IntegrationPrivacy.Permissions
            ]);
}
