namespace SecretBase.Core.Assistant;

/// <summary>Heuristic intent classifier. Keeps Question vs ActionRequest separate.</summary>
public static class AssistantIntentClassifier
{
    private static readonly string[] ActionMarkers =
    [
        "開いて", "開けて", "起動", "始めて", "始めよう", "はじめよう", "再生", "プレイ",
        "run", "open", "launch", "start", "play", "execute"
    ];

    private static readonly string[] QuestionMarkers =
    [
        "？", "?", "教えて", "なに", "何", "どう", "いつ", "どれ", "どこ",
        "what", "when", "which", "how", "why", "tell me", "show me"
    ];

    private static readonly string[] SuggestMarkers =
    [
        "おすすめ", "優先", "候補", "すべき", "やればいい", "どうする",
        "recommend", "suggest", "should i", "priority"
    ];

    public static AssistantIntentKind Classify(string? userText)
    {
        var text = userText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return AssistantIntentKind.Question;
        }

        var lower = text.ToLowerInvariant();
        var hasAction = ActionMarkers.Any(m =>
            text.Contains(m, StringComparison.OrdinalIgnoreCase)
            || lower.Contains(m, StringComparison.Ordinal));
        var hasQuestion = QuestionMarkers.Any(m =>
            text.Contains(m, StringComparison.OrdinalIgnoreCase)
            || lower.Contains(m, StringComparison.Ordinal));
        var hasSuggest = SuggestMarkers.Any(m =>
            text.Contains(m, StringComparison.OrdinalIgnoreCase)
            || lower.Contains(m, StringComparison.Ordinal));

        if (hasAction && !LooksLikeAboutOnly(text))
        {
            return AssistantIntentKind.ActionRequest;
        }

        if (hasSuggest)
        {
            return AssistantIntentKind.Suggestion;
        }

        if (hasQuestion || text.Contains("について", StringComparison.Ordinal))
        {
            return AssistantIntentKind.Question;
        }

        return AssistantIntentKind.RequestInfo;
    }

    /// <summary>"Pokemonについて教えて" must not become ActionRequest.</summary>
    private static bool LooksLikeAboutOnly(string text) =>
        (text.Contains("について", StringComparison.Ordinal)
         || text.Contains("教えて", StringComparison.Ordinal)
         || text.Contains("about ", StringComparison.OrdinalIgnoreCase))
        && !text.Contains("開いて", StringComparison.Ordinal)
        && !text.Contains("始めて", StringComparison.OrdinalIgnoreCase)
        && !text.Contains("open", StringComparison.OrdinalIgnoreCase);
}
