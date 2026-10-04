using System.Text.Json;

namespace SecretBase.Core.Jev;

/// <summary>
/// Request shape for the documented Jev decision API.
/// Hosted keys (<c>jv_live_</c>) use <see cref="HostedEndpoint"/>.
/// Other keys use the official TypeSafe endpoint <see cref="OfficialEndpoint"/>.
/// The two hosts share one request shape: model, state, and typed questions.
/// </summary>
public static class JevDecisionContract
{
    public const string Model = "jev-latest";

    public const string HostedEndpoint = "https://jevtypesafeai.com/api/v1/decide";

    public const string OfficialEndpoint = "https://api.typesafe.ai/v1/systemone";

    public const string HostedKeyPrefix = "jv_live_";

    public static string ResolveEndpoint(string apiKey)
    {
        var key = apiKey.Trim();
        return key.StartsWith(HostedKeyPrefix, StringComparison.Ordinal)
            ? HostedEndpoint
            : OfficialEndpoint;
    }

    public static string BuildDecisionRequest(string state) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["model"] = Model,
            ["state"] = state,
            ["questions"] = DecisionQuestions()
        });

    public static string BuildConnectionCheckRequest() =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["model"] = Model,
            ["state"] = "Secret Base connection check.",
            ["questions"] = new Dictionary<string, object?>
            {
                ["ready"] = new Dictionary<string, object?>
                {
                    ["type"] = "noul",
                    ["instructions"] = "Is this a connection check?"
                }
            }
        });

    private static Dictionary<string, object?> DecisionQuestions() =>
        new()
        {
            ["situation"] = Choice(
                "What is the user trying to do in Secret Base?",
                new Dictionary<string, string>
                {
                    ["study"] = "studying, reading, or learning",
                    ["coding"] = "writing or debugging code",
                    ["creative"] = "design, writing, or making art",
                    ["music"] = "listening to or changing music",
                    ["communication"] = "messages, mail, or meetings",
                    ["entertainment"] = "games or casual media",
                    ["break"] = "resting or stepping away",
                    ["unknown"] = "not enough information"
                }),
            ["next_step"] = Choice(
                "What should Secret Base do next? Choose only one of the criteria. Do not invent a command.",
                new Dictionary<string, string>
                {
                    ["none"] = "no Secret Base action",
                    ["suggest"] = "suggest only, do not run anything",
                    ["prepare_workspace"] = "prepare the registered workspace display",
                    ["continue_workspace"] = "continue the current registered workspace",
                    ["start_focus"] = "start a focus timer",
                    ["ask_confirmation"] = "ask the user before any action"
                }),
            ["gate"] = Choice(
                "Before any automation, what is the gate?",
                new Dictionary<string, string>
                {
                    ["allow"] = "Secret Base may offer a safe action through its own confirmation policy",
                    ["confirm"] = "ask the user before running",
                    ["deny"] = "do not run"
                })
        };

    private static Dictionary<string, object?> Choice(string instructions, Dictionary<string, string> criteria) =>
        new()
        {
            ["type"] = "choice",
            ["instructions"] = instructions,
            ["criteria"] = criteria
        };
}
