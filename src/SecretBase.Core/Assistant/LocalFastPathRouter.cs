using System.Text.Json;
using System.Text.RegularExpressions;

namespace SecretBase.Core.Assistant;

/// <summary>How far a local match can go without Jev / conversation models.</summary>
public enum LocalFastPathKind
{
    /// <summary>No clear local match — may need Jev and/or a conversation model.</summary>
    None = 0,

    /// <summary>Ask the user for a missing argument. Do not call Jev or a model.</summary>
    Clarify = 1,

    /// <summary>Run ReadOnly / Suggest / SafeAuto tools locally.</summary>
    RunTools = 2,

    /// <summary>Queue RequiresConfirmation tools without a model round.</summary>
    ConfirmTools = 3,

    /// <summary>Needs Jev judgment (situation / next_step). Do not invent a tool yet.</summary>
    NeedsJudgment = 4
}

public sealed class LocalFastPathStep
{
    public required string ToolName { get; init; }

    public string ArgumentsJson { get; init; } = "{}";

    public string Label { get; init; } = string.Empty;
}

/// <summary>Result of matching a clear natural-language request to Secret Base tools.</summary>
public sealed class LocalFastPathMatch
{
    public required LocalFastPathKind Kind { get; init; }

    public string RouteId { get; init; } = "none";

    public double Confidence { get; init; }

    public string? ClarifyQuestion { get; init; }

    public IReadOnlyList<LocalFastPathStep> Steps { get; init; } = Array.Empty<LocalFastPathStep>();

    public static LocalFastPathMatch None() =>
        new() { Kind = LocalFastPathKind.None, RouteId = "none", Confidence = 0 };

    public static LocalFastPathMatch Clarify(string routeId, string question) =>
        new()
        {
            Kind = LocalFastPathKind.Clarify,
            RouteId = routeId,
            Confidence = 0.9,
            ClarifyQuestion = question
        };

    public static LocalFastPathMatch Run(string routeId, params LocalFastPathStep[] steps) =>
        new()
        {
            Kind = LocalFastPathKind.RunTools,
            RouteId = routeId,
            Confidence = 0.95,
            Steps = steps
        };

    public static LocalFastPathMatch Confirm(string routeId, params LocalFastPathStep[] steps) =>
        new()
        {
            Kind = LocalFastPathKind.ConfirmTools,
            RouteId = routeId,
            Confidence = 0.92,
            Steps = steps
        };

    public static LocalFastPathMatch Judgment(string routeId) =>
        new()
        {
            Kind = LocalFastPathKind.NeedsJudgment,
            RouteId = routeId,
            Confidence = 0.7
        };
}

/// <summary>
/// Maps clear Japanese/English requests onto existing tools without calling Jev or an LLM.
/// Prefers false negatives (None) over wrong execution.
/// </summary>
public static partial class LocalFastPathRouter
{
    public static LocalFastPathMatch TryMatch(
        string? userText,
        AssistantIntentKind intent,
        AssistantContextSnapshot? snapshot = null)
    {
        var text = userText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 2000)
        {
            return LocalFastPathMatch.None();
        }

        // Judgment-first: situational / multi-workflow phrases must not be forced into a tool.
        if (LooksLikeSituationalJudgment(text))
        {
            return LocalFastPathMatch.Judgment("situational");
        }

        if (LooksLikeMusicPause(text))
        {
            return LocalFastPathMatch.Run(
                "music_pause",
                Step(AssistantToolNames.MusicPause, "{}", "Pause music"));
        }

        if (LooksLikeMusicState(text))
        {
            return LocalFastPathMatch.Run(
                "music_state",
                Step(AssistantToolNames.MusicGetState, "{}", "Music state"));
        }

        if (LooksLikeTodayAgenda(text))
        {
            return LocalFastPathMatch.Run(
                "calendar_today",
                Step(AssistantToolNames.CalendarGetToday, "{}", "Today's agenda"));
        }

        if (LooksLikeTomorrowAgenda(text))
        {
            return LocalFastPathMatch.Run(
                "calendar_tomorrow",
                Step(AssistantToolNames.CalendarGetUpcoming, """{"days":1}""", "Tomorrow / next day"));
        }

        if (LooksLikeUpcomingAgenda(text))
        {
            return LocalFastPathMatch.Run(
                "calendar_upcoming",
                Step(AssistantToolNames.CalendarGetUpcoming, """{"days":7}""", "Upcoming agenda"));
        }

        if (LooksLikeProjectList(text))
        {
            return LocalFastPathMatch.Run(
                "projects_list",
                Step(AssistantToolNames.CreativeListProjects, "{}", "List projects"));
        }

        if (TryMatchProjectLookup(text, snapshot, out var projectMatch))
        {
            return projectMatch;
        }

        if (LooksLikeTodoList(text))
        {
            return LocalFastPathMatch.Run(
                "todo_list",
                Step(AssistantToolNames.TodoList, "{}", "List todos"));
        }

        if (TryMatchTodoAdd(text, out var todoMatch))
        {
            return todoMatch;
        }

        if (LooksLikeAppsList(text))
        {
            return LocalFastPathMatch.Run(
                "apps_list",
                Step(AssistantToolNames.AppsList, "{}", "List apps"));
        }

        if (LooksLikeUsualApply(text))
        {
            return LocalFastPathMatch.Confirm(
                "calendar_apply_usual",
                Step(AssistantToolNames.CalendarApplyUsual, "{}", "Apply usual schedule"));
        }

        if (LooksLikeWorkspacePrepareOnly(text))
        {
            return LocalFastPathMatch.Run(
                "workspace_prepare",
                Step(AssistantToolNames.WorkspacePrepare, """{"intent":"continue"}""", "Prepare workspace"));
        }

        // Timed schedule writes stay on LocalScheduleParser in AssistantService (day offset aware).
        // Clear ActionRequest with no local match still may need a model — not judgment by default.
        _ = intent;
        return LocalFastPathMatch.None();
    }

    /// <summary>True when Jev should run before (or instead of) a conversation model.</summary>
    public static bool NeedsJev(LocalFastPathMatch match, AssistantIntentKind intent, string? userText)
    {
        _ = intent;
        if (match.Kind == LocalFastPathKind.NeedsJudgment)
        {
            return true;
        }

        if (match.Kind is LocalFastPathKind.RunTools
            or LocalFastPathKind.ConfirmTools
            or LocalFastPathKind.Clarify)
        {
            return false;
        }

        // Situational / multi-workflow phrases only. ActionRequests that still need a
        // conversation model consult Jev as a gate in AssistantService (not here).
        return LooksLikeSituationalJudgment(userText ?? string.Empty);
    }

    /// <summary>True when a conversation model should still be consulted.</summary>
    public static bool NeedsConversationModel(LocalFastPathMatch match) =>
        match.Kind is LocalFastPathKind.None or LocalFastPathKind.NeedsJudgment;

    private static LocalFastPathStep Step(string tool, string args, string label) =>
        new() { ToolName = tool, ArgumentsJson = args, Label = label };

    private static bool LooksLikeSituationalJudgment(string text) =>
        text.Contains("いつもの環境", StringComparison.Ordinal)
        || text.Contains("いつもの勉強環境", StringComparison.Ordinal)
        || text.Contains("usual environment", StringComparison.OrdinalIgnoreCase)
        || text.Contains("続きから", StringComparison.Ordinal)
        || text.Contains("つづきから", StringComparison.Ordinal)
        || text.Contains("continue where", StringComparison.OrdinalIgnoreCase)
        || text.Contains("どうすれば", StringComparison.Ordinal)
        || text.Contains("何から", StringComparison.Ordinal)
        || text.Contains("なにから", StringComparison.Ordinal)
        || ((text.Contains("環境にして", StringComparison.Ordinal)
             || text.Contains("環境を整えて", StringComparison.Ordinal))
            && !text.Contains("プログラミング環境", StringComparison.Ordinal)
            && !text.Contains("開発環境", StringComparison.Ordinal)
            && !text.Contains("コーディング環境", StringComparison.Ordinal));

    private static bool LooksLikeMusicPause(string text) =>
        ((text.Contains("音楽", StringComparison.Ordinal)
          || text.Contains("曲", StringComparison.Ordinal)
          || text.Contains("music", StringComparison.OrdinalIgnoreCase)
          || text.Contains("spotify", StringComparison.OrdinalIgnoreCase))
         && (text.Contains("止めて", StringComparison.Ordinal)
             || text.Contains("停止", StringComparison.Ordinal)
             || text.Contains("ストップ", StringComparison.Ordinal)
             || text.Contains("pause", StringComparison.OrdinalIgnoreCase)
             || text.Contains("stop", StringComparison.OrdinalIgnoreCase)))
        || text.Equals("pause", StringComparison.OrdinalIgnoreCase)
        || text.Equals("stop", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMusicState(string text) =>
        text.Contains("何流れて", StringComparison.Ordinal)
        || text.Contains("なに流れて", StringComparison.Ordinal)
        || text.Contains("再生状態", StringComparison.Ordinal)
        || text.Contains("音楽の状態", StringComparison.Ordinal)
        || text.Contains("now playing", StringComparison.OrdinalIgnoreCase)
        || text.Contains("what's playing", StringComparison.OrdinalIgnoreCase)
        || text.Contains("whats playing", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeTodayAgenda(string text) =>
        text.Contains("今日の予定", StringComparison.Ordinal)
        || text.Contains("きょうの予定", StringComparison.Ordinal)
        || text.Contains("今日のスケジュール", StringComparison.Ordinal)
        || text.Contains("today's agenda", StringComparison.OrdinalIgnoreCase)
        || text.Contains("todays agenda", StringComparison.OrdinalIgnoreCase)
        || text.Contains("today agenda", StringComparison.OrdinalIgnoreCase)
        || text.Contains("today's schedule", StringComparison.OrdinalIgnoreCase)
        || text.Contains("today schedule", StringComparison.OrdinalIgnoreCase)
        || (text.Contains("今日", StringComparison.Ordinal)
            && (text.Contains("予定", StringComparison.Ordinal)
                || text.Contains("スケジュール", StringComparison.Ordinal))
            && LooksLikeShow(text));

    private static bool LooksLikeTomorrowAgenda(string text) =>
        text.Contains("明日の予定", StringComparison.Ordinal)
        || text.Contains("あしたの予定", StringComparison.Ordinal)
        || text.Contains("tomorrow's schedule", StringComparison.OrdinalIgnoreCase)
        || text.Contains("tomorrow schedule", StringComparison.OrdinalIgnoreCase)
        || text.Contains("tomorrow's agenda", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeUpcomingAgenda(string text) =>
        text.Contains("今後の予定", StringComparison.Ordinal)
        || text.Contains("これからの予定", StringComparison.Ordinal)
        || text.Contains("今週の予定", StringComparison.Ordinal)
        || ((text.Contains("upcoming", StringComparison.OrdinalIgnoreCase)
             || text.Contains("coming up", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("event", StringComparison.OrdinalIgnoreCase)
                || text.Contains("schedule", StringComparison.OrdinalIgnoreCase)
                || text.Contains("agenda", StringComparison.OrdinalIgnoreCase)
                || text.Contains("予定", StringComparison.Ordinal)));

    private static bool LooksLikeProjectList(string text) =>
        text.Contains("プロジェクト一覧", StringComparison.Ordinal)
        || text.Contains("プロジェクトリスト", StringComparison.Ordinal)
        || text.Contains("プロジェクトを一覧", StringComparison.Ordinal)
        || text.Contains("list projects", StringComparison.OrdinalIgnoreCase)
        || text.Contains("show projects", StringComparison.OrdinalIgnoreCase)
        || (text.Contains("プロジェクト", StringComparison.Ordinal)
            && LooksLikeShow(text)
            && !text.Contains("開いて", StringComparison.Ordinal)
            && !text.Contains("検索", StringComparison.Ordinal));

    private static bool TryMatchProjectLookup(
        string text,
        AssistantContextSnapshot? snapshot,
        out LocalFastPathMatch match)
    {
        match = LocalFastPathMatch.None();
        var wantsLookup = text.Contains("プロジェクト", StringComparison.Ordinal)
                          || text.Contains("project", StringComparison.OrdinalIgnoreCase);
        if (!wantsLookup)
        {
            return false;
        }

        var search = text.Contains("検索", StringComparison.Ordinal)
                     || text.Contains("search", StringComparison.OrdinalIgnoreCase)
                     || text.Contains("詳しく", StringComparison.Ordinal)
                     || text.Contains("詳細", StringComparison.Ordinal)
                     || text.Contains("detail", StringComparison.OrdinalIgnoreCase);
        if (!search && !text.Contains("このプロジェクト", StringComparison.Ordinal))
        {
            return false;
        }

        if (snapshot?.Projects is not { Count: > 0 })
        {
            if (search)
            {
                match = LocalFastPathMatch.Clarify(
                    "project_lookup",
                    "Which project? Tell me the registered project name.");
                return true;
            }

            return false;
        }

        var hits = snapshot.Projects
            .Where(p => !string.IsNullOrWhiteSpace(p.Name)
                        && text.Contains(p.Name, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        if (hits.Count == 1)
        {
            var id = hits[0].Id;
            var args = JsonSerializer.Serialize(new Dictionary<string, string> { ["project_id"] = id });
            match = LocalFastPathMatch.Run(
                "project_get",
                Step(AssistantToolNames.CreativeGetProject, args, $"Project {hits[0].Name}"));
            return true;
        }

        if (hits.Count > 1)
        {
            match = LocalFastPathMatch.Clarify(
                "project_lookup",
                "Several projects match. Which one: "
                + string.Join(", ", hits.Select(h => h.Name))
                + "?");
            return true;
        }

        if (text.Contains("このプロジェクト", StringComparison.Ordinal) || search)
        {
            match = LocalFastPathMatch.Run(
                "projects_list",
                Step(AssistantToolNames.CreativeListProjects, "{}", "List projects"));
            return true;
        }

        return false;
    }

    private static bool LooksLikeTodoList(string text) =>
        text.Contains("Todo一覧", StringComparison.OrdinalIgnoreCase)
        || text.Contains("TODO一覧", StringComparison.OrdinalIgnoreCase)
        || text.Contains("todo list", StringComparison.OrdinalIgnoreCase)
        || text.Contains("タスク一覧", StringComparison.Ordinal)
        || text.Contains("やること一覧", StringComparison.Ordinal)
        || (text.Contains("todos", StringComparison.OrdinalIgnoreCase)
            && LooksLikeShow(text)
            && !text.Contains("追加", StringComparison.Ordinal)
            && !text.Contains("add", StringComparison.OrdinalIgnoreCase))
        || ((text.Contains("Todo", StringComparison.OrdinalIgnoreCase)
             || text.Contains("TODO", StringComparison.Ordinal)
             || text.Contains("タスク", StringComparison.Ordinal))
            && LooksLikeShow(text)
            && !text.Contains("追加", StringComparison.Ordinal)
            && !text.Contains("add", StringComparison.OrdinalIgnoreCase));

    private static bool TryMatchTodoAdd(string text, out LocalFastPathMatch match)
    {
        match = LocalFastPathMatch.None();
        var adding = text.Contains("追加", StringComparison.Ordinal)
                     || text.Contains("add", StringComparison.OrdinalIgnoreCase);
        var todoish = text.Contains("Todo", StringComparison.OrdinalIgnoreCase)
                      || text.Contains("TODO", StringComparison.Ordinal)
                      || text.Contains("タスク", StringComparison.Ordinal)
                      || text.Contains("やること", StringComparison.Ordinal);
        if (!adding || !todoish)
        {
            return false;
        }

        var title = ExtractQuoted(text)
                    ?? ExtractTodoTitle(text);
        if (string.IsNullOrWhiteSpace(title))
        {
            match = LocalFastPathMatch.Clarify(
                "todo_add",
                "What should I add to Todo? Give a short title.");
            return true;
        }

        var args = JsonSerializer.Serialize(new Dictionary<string, string> { ["title"] = title });
        match = LocalFastPathMatch.Confirm(
            "todo_add",
            Step(AssistantToolNames.TodoAdd, args, $"Add todo: {title}"));
        return true;
    }

    private static bool LooksLikeAppsList(string text) =>
        text.Contains("アプリ一覧", StringComparison.Ordinal)
        || text.Contains("My Apps", StringComparison.OrdinalIgnoreCase)
        || text.Contains("list apps", StringComparison.OrdinalIgnoreCase)
        || (text.Contains("アプリ", StringComparison.Ordinal)
            && LooksLikeShow(text)
            && !text.Contains("開いて", StringComparison.Ordinal)
            && !text.Contains("起動", StringComparison.Ordinal));

    private static bool LooksLikeUsualApply(string text) =>
        (text.Contains("いつも", StringComparison.Ordinal) || text.Contains("usual", StringComparison.OrdinalIgnoreCase))
        && (text.Contains("予定", StringComparison.Ordinal)
            || text.Contains("スケジュール", StringComparison.Ordinal)
            || text.Contains("schedule", StringComparison.OrdinalIgnoreCase))
        && (text.Contains("入れて", StringComparison.Ordinal)
            || text.Contains("いれて", StringComparison.Ordinal)
            || text.Contains("適用", StringComparison.Ordinal)
            || text.Contains("apply", StringComparison.OrdinalIgnoreCase)
            || text.Contains("反映", StringComparison.Ordinal));

    private static bool LooksLikeWorkspacePrepareOnly(string text) =>
        text.Contains("ワークスペースを準備", StringComparison.Ordinal)
        || text.Contains("workspace prepare", StringComparison.OrdinalIgnoreCase)
        || text.Contains("作業場を準備", StringComparison.Ordinal);

    private static bool LooksLikeShow(string text) =>
        text.Contains("見せて", StringComparison.Ordinal)
        || text.Contains("見せろ", StringComparison.Ordinal)
        || text.Contains("教えて", StringComparison.Ordinal)
        || text.Contains("確認", StringComparison.Ordinal)
        || text.Contains("一覧", StringComparison.Ordinal)
        || text.Contains("リスト", StringComparison.Ordinal)
        || text.Contains("show", StringComparison.OrdinalIgnoreCase)
        || text.Contains("list", StringComparison.OrdinalIgnoreCase)
        || text.Contains("tell me", StringComparison.OrdinalIgnoreCase)
        || text.Contains("？", StringComparison.Ordinal)
        || text.Contains("?", StringComparison.Ordinal);

    private static string? ExtractQuoted(string text)
    {
        var m = QuotedTitle().Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractTodoTitle(string text)
    {
        var m = TodoTitlePattern().Match(text);
        if (!m.Success)
        {
            return null;
        }

        var title = m.Groups[1].Value.Trim().Trim('を', 'に', 'へ', 'は');
        title = TodoNoise().Replace(title, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(title) || title.Length > 120 ? null : title;
    }

    [GeneratedRegex("[「\"']([^」\"']{1,120})[」\"']", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedTitle();

    [GeneratedRegex(
        @"(?:Todo|TODO|タスク|やること)\s*(?:に|へ)?\s*(?:追加|add)\s*(?:して)?\s*[:：]?\s*(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TodoTitlePattern();

    [GeneratedRegex(
        @"(を)?\s*(追加して|追加|add)(ください|下さい)?[.。!！？\s　]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TodoNoise();
}
