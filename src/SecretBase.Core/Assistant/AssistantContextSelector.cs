namespace SecretBase.Core.Assistant;

/// <summary>Picks minimal context scopes from intent / wording. Avoids dumping All every turn.</summary>
public static class AssistantContextSelector
{
    public static AssistantContextScope FromUserText(string? userText, AssistantIntentKind intent)
    {
        var text = userText?.Trim() ?? string.Empty;
        var lower = text.ToLowerInvariant();
        var scope = AssistantContextScope.None;

        if (ContainsAny(lower, text, "予定", "カレンダー", "今日", "空き", "schedule", "calendar", "today", "agenda"))
        {
            scope |= AssistantContextScope.Calendar;
        }

        if (ContainsAny(lower, text, "プロジェクト", "pokemon", "創作", "project", "creative", "cursor", "作業"))
        {
            scope |= AssistantContextScope.Creative;
        }

        if (ContainsAny(lower, text, "アプリ", "app", "my apps"))
        {
            scope |= AssistantContextScope.Apps;
        }

        if (ContainsAny(lower, text, "音楽", "music", "曲", "再生", "play"))
        {
            scope |= AssistantContextScope.Music;
        }

        if (ContainsAny(lower, text, "classroom", "クラスルーム", "連携", "integration"))
        {
            scope |= AssistantContextScope.Integrations;
        }

        if (ContainsAny(lower, text, "設定", "provider", "api", "ai"))
        {
            scope |= AssistantContextScope.Provider;
        }

        if (scope == AssistantContextScope.None)
        {
            scope = intent switch
            {
                AssistantIntentKind.Suggestion => AssistantContextScope.Calendar | AssistantContextScope.Creative,
                AssistantIntentKind.ActionRequest => AssistantContextScope.Creative | AssistantContextScope.Apps,
                _ => AssistantContextScope.Calendar | AssistantContextScope.Creative
            };
        }

        // Combined prioritization questions need both calendar + projects.
        if (ContainsAny(lower, text, "優先", "やればいい", "何をすれ", "should", "priority", "作業モード"))
        {
            scope |= AssistantContextScope.Calendar | AssistantContextScope.Creative;
        }

        if (ContainsAny(lower, text, "作業モード", "work mode"))
        {
            scope |= AssistantContextScope.Calendar | AssistantContextScope.Creative | AssistantContextScope.Music;
        }

        return scope;
    }

    private static bool ContainsAny(string lower, string original, params string[] markers) =>
        markers.Any(m =>
            original.Contains(m, StringComparison.OrdinalIgnoreCase)
            || lower.Contains(m, StringComparison.Ordinal));
}
