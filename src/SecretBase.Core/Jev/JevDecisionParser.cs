using System.Text.Json;

namespace SecretBase.Core.Jev;

/// <summary>Reads a documented Jev <c>answers</c> object. Values outside the criteria are invalid.</summary>
public static class JevDecisionParser
{
    public static JevDecision Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return Invalid("Jev returned an empty body.");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
            {
                return Invalid("Jev response has no answers object.");
            }

            var model = root.TryGetProperty("model", out var modelNode) && modelNode.ValueKind == JsonValueKind.String
                ? modelNode.GetString()
                : null;

            if (!TryChoice(answers, "situation", JevSituationMap, out var situation, out var situationConfidence, out var rejected))
            {
                return Invalid("Jev situation was not one of the allowed choices.", rejected, model);
            }

            if (!TryChoice(answers, "next_step", JevNextStepMap, out var next, out var nextConfidence, out rejected))
            {
                return Invalid("Jev next step was not one of the allowed choices.", rejected, model);
            }

            if (!TryChoice(answers, "gate", JevGateMap, out var gate, out var gateConfidence, out rejected))
            {
                return Invalid("Jev gate was not allow, confirm, or deny.", rejected, model);
            }

            return new JevDecision
            {
                IsValid = true,
                Situation = situation,
                NextStep = next,
                Gate = gate,
                SituationConfidence = situationConfidence,
                NextStepConfidence = nextConfidence,
                GateConfidence = gateConfidence,
                Model = model
            };
        }
        catch (JsonException)
        {
            return Invalid("Jev returned invalid JSON.");
        }
    }

    public static bool IsConnectionCheck(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("answers", out var answers)
                || !answers.TryGetProperty("ready", out var ready)
                || ready.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return ready.TryGetProperty("type", out var type)
                   && type.ValueKind == JsonValueKind.String
                   && string.Equals(type.GetString(), "noul", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? ReadModel(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.String
                ? model.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryChoice<T>(
        JsonElement answers,
        string name,
        IReadOnlyDictionary<string, T> map,
        out T value,
        out double? confidence,
        out string? rejected) where T : struct
    {
        value = default;
        confidence = null;
        rejected = null;
        if (!answers.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Object)
        {
            rejected = null;
            return false;
        }

        if (!node.TryGetProperty("type", out var typeNode)
            || typeNode.ValueKind != JsonValueKind.String
            || !string.Equals(typeNode.GetString(), "choice", StringComparison.Ordinal))
        {
            return false;
        }

        if (!node.TryGetProperty("choice", out var choiceNode) || choiceNode.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var choice = choiceNode.GetString() ?? string.Empty;
        if (!map.TryGetValue(choice, out value))
        {
            rejected = choice;
            return false;
        }

        if (node.TryGetProperty("confidence", out var confidenceNode)
            && confidenceNode.ValueKind == JsonValueKind.Number
            && confidenceNode.TryGetDouble(out var parsed))
        {
            confidence = parsed;
        }

        return true;
    }

    private static JevDecision Invalid(string reason, string? rejected = null, string? model = null) =>
        new()
        {
            IsValid = false,
            InvalidReason = reason,
            RejectedValue = rejected,
            Model = model,
            Gate = JevGate.Deny,
            NextStep = JevNextStep.None,
            Situation = JevSituation.Unknown
        };

    private static readonly Dictionary<string, JevSituation> JevSituationMap = new(StringComparer.Ordinal)
    {
        ["study"] = JevSituation.Study,
        ["coding"] = JevSituation.Coding,
        ["creative"] = JevSituation.Creative,
        ["music"] = JevSituation.Music,
        ["communication"] = JevSituation.Communication,
        ["entertainment"] = JevSituation.Entertainment,
        ["break"] = JevSituation.Break,
        ["unknown"] = JevSituation.Unknown
    };

    private static readonly Dictionary<string, JevNextStep> JevNextStepMap = new(StringComparer.Ordinal)
    {
        ["none"] = JevNextStep.None,
        ["suggest"] = JevNextStep.Suggest,
        ["prepare_workspace"] = JevNextStep.PrepareWorkspace,
        ["continue_workspace"] = JevNextStep.ContinueWorkspace,
        ["start_focus"] = JevNextStep.StartFocus,
        ["ask_confirmation"] = JevNextStep.AskConfirmation
    };

    private static readonly Dictionary<string, JevGate> JevGateMap = new(StringComparer.Ordinal)
    {
        ["allow"] = JevGate.Allow,
        ["confirm"] = JevGate.Confirm,
        ["deny"] = JevGate.Deny
    };
}
