using SecretBase.Core.Assistant;

namespace SecretBase.Core.Jev;

/// <summary>
/// Deterministic decision used when Jev is missing or unreachable.
/// Uncertain action requests ask for confirmation. Nothing is auto-run from here.
/// </summary>
public static class JevDecisionFallback
{
    public static JevDecision FromObservation(JevObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var next = observation.Intent switch
        {
            AssistantIntentKind.ActionRequest => JevNextStep.AskConfirmation,
            AssistantIntentKind.Suggestion => JevNextStep.Suggest,
            _ => JevNextStep.None
        };
        var gate = next == JevNextStep.AskConfirmation ? JevGate.Confirm : JevGate.Allow;
        return new JevDecision
        {
            IsValid = true,
            FromFallback = true,
            Situation = ClassifySituation(observation.Message),
            NextStep = next,
            Gate = gate
        };
    }

    public static JevSituation ClassifySituation(string? message)
    {
        var text = message ?? string.Empty;
        if (ContainsAny(text, "曲", "音楽", "music", "spotify", "再生", "プレイ"))
        {
            return JevSituation.Music;
        }

        if (ContainsAny(text, "勉強", "学習", "study", "homework", "宿題"))
        {
            return JevSituation.Study;
        }

        if (ContainsAny(text, "コード", "プログラミング", "code", "debug", "cursor"))
        {
            return JevSituation.Coding;
        }

        if (ContainsAny(text, "デザイン", "creative", "design", "イラスト"))
        {
            return JevSituation.Creative;
        }

        if (ContainsAny(text, "メール", "会議", "mail", "meeting", "slack"))
        {
            return JevSituation.Communication;
        }

        if (ContainsAny(text, "ゲーム", "game", "youtube", "動画"))
        {
            return JevSituation.Entertainment;
        }

        if (ContainsAny(text, "休憩", "休む", "break", "rest"))
        {
            return JevSituation.Break;
        }

        return JevSituation.Unknown;
    }

    private static bool ContainsAny(string text, params string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
