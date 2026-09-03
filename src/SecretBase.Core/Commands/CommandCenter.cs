using SecretBase.Core.Attention;
using SecretBase.Core.Briefing;
using SecretBase.Core.Capture;
using SecretBase.Core.Connectors;
using SecretBase.Core.Creative;
using SecretBase.Core.Explain;
using SecretBase.Core.Files;
using SecretBase.Core.Intent;
using SecretBase.Core.Privacy;
using SecretBase.Core.Projects;
using SecretBase.Core.Search;
using SecretBase.Core.Situation;
using SecretBase.Core.Timeline;
using SecretBase.Core.Workspace;

namespace SecretBase.Core.Commands;

public enum CommandKind
{
    Unknown = 0,
    Briefing = 1,
    Continue = 2,
    Focus = 3,
    Search = 4,
    Capture = 5,
    Timeline = 6,
    Explain = 7,
    Cleanup = 8,
    Calendar = 9,
    Tasks = 10,
    Memory = 11,
    Palette = 12,
    Privacy = 13,
    Attention = 14,
    Project = 15,
    Integrations = 16
}

public sealed class CommandDispatch
{
    public CommandKind Kind { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public bool HandledWithoutLlm { get; init; }

    public bool RequiresConfirmation { get; init; }

    public string? ToolHint { get; init; }

    public DetectedIntent? Intent { get; init; }
}

/// <summary>
/// Deterministic Command Center. LLM is a last resort, never the first hop for briefing/search/focus/continue.
/// </summary>
public static class CommandCenter
{
    public static CommandDispatch Handle(string utterance, ICommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = (utterance ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Palette(context, text);
        }

        if (Looks(text, "今日何すれば", "今日の予定", "briefing", "おはよう", "good morning", "good evening", "daily briefing"))
        {
            var briefing = context.Briefing();
            return new CommandDispatch
            {
                Kind = CommandKind.Briefing,
                Title = "Daily briefing",
                Body = briefing.Format(),
                HandledWithoutLlm = true,
                Intent = context.DetectIntent(text)
            };
        }

        if (Looks(text, "昨日の続き", "続き", "continue", "resume", "前回", "yesterday", "where did i"))
        {
            var intent = context.DetectIntent(text);
            var continuation = context.Continuation();
            return new CommandDispatch
            {
                Kind = CommandKind.Continue,
                Title = "Continue " + (continuation.ProjectName ?? "previous session"),
                Body = continuation.Format()
                    + Environment.NewLine
                    + Environment.NewLine
                    + IntentExplainer.Explain(intent, context.ComposeSituation()),
                HandledWithoutLlm = true,
                RequiresConfirmation = true,
                ToolHint = "workspace_continue",
                Intent = intent
            };
        }

        if (Looks(text, "集中", "pomodoro", "focus", "30分", "ポモドーロ"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Focus,
                Title = "Start focus",
                Body = "Start a local focus timer. No apps will launch.",
                HandledWithoutLlm = true,
                ToolHint = "focus_start",
                Intent = context.DetectIntent(text)
            };
        }

        if (Looks(text, "なんで", "なぜ", "why did you", "why this", "どうして提案"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Explain,
                Title = "Why this suggestion",
                Body = IntentExplainer.Explain(context.DetectIntent(text), context.ComposeSituation()),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "今日何してた", "timeline", "what did i do", "activity"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Timeline,
                Title = "Today's activity",
                Body = ActivityTimeline.Format(context.Timeline()),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "整理", "使ってない", "cleanup", "unused", "duplicate"))
        {
            var files = FileIntelligence.SuggestCleanup(context.ListProjects(), context.Now);
            return new CommandDispatch
            {
                Kind = CommandKind.Cleanup,
                Title = "File review",
                Body = FileIntelligence.FormatSuggestion(files),
                HandledWithoutLlm = true,
                RequiresConfirmation = false,
                ToolHint = "files_suggest_cleanup"
            };
        }

        if (Looks(text, "what does secret base remember", "覚えてる", "memory list", "memories"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Memory,
                Title = "Memory",
                Body = context.MemoryCatalog(),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "探して", "search", "この前", "where did i"))
        {
            var hits = context.Search(text);
            return new CommandDispatch
            {
                Kind = CommandKind.Search,
                Title = "Search",
                Body = BaseSearch.Format(hits),
                HandledWithoutLlm = true,
                Intent = context.DetectIntent(text)
            };
        }

        if (Looks(text, "キャプチャ", "capture", "メモ", "idea", "思いつ"))
        {
            var draft = QuickCaptureClassifier.Classify(text, context.ListProjects().Select(p => p.Name).ToList());
            return new CommandDispatch
            {
                Kind = CommandKind.Capture,
                Title = "Quick capture (" + draft.Destination + ")",
                Body = draft.Text + Environment.NewLine + draft.Reason,
                HandledWithoutLlm = true,
                ToolHint = "quick_capture"
            };
        }

        if (Looks(text, "予定を整理", "calendar", "明日", "tomorrow", "今日の予定"))
        {
            var briefing = context.Briefing();
            return new CommandDispatch
            {
                Kind = CommandKind.Calendar,
                Title = "Calendar",
                Body = string.Join(Environment.NewLine, briefing.CalendarLines.DefaultIfEmpty("Quiet")),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "privacy", "観測", "never collect", "what does secret base observe"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Privacy,
                Title = "Privacy",
                Body = PrivacyManifest.Format()
                    + Environment.NewLine
                    + Environment.NewLine
                    + context.IntegrationPermissions(),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "integration", "インテグレーション", "tetris", "my app", "自分のアプリ", "コネクタ"))
        {
            var read = context.TryIntegrationRead(text);
            if (read is not null)
            {
                return new CommandDispatch
                {
                    Kind = CommandKind.Integrations,
                    Title = read.NeedsConfirmation ? "Confirm integration action" : "Integration",
                    Body = read.Message,
                    HandledWithoutLlm = true,
                    RequiresConfirmation = read.NeedsConfirmation,
                    ToolHint = read.NeedsConfirmation ? "integration_invoke" : "integration_query"
                };
            }

            return new CommandDispatch
            {
                Kind = CommandKind.Integrations,
                Title = "My Integrations",
                Body = context.IntegrationsCatalog(),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "attention", "通知", "overdue", "what needs me"))
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Attention,
                Title = "Attention",
                Body = AttentionCenter.Format(context.Attention()),
                HandledWithoutLlm = true
            };
        }

        if (Looks(text, "どうなってる", "how's", "project status", "what about"))
        {
            var name = context.ListProjects()
                .Select(item => item.Name)
                .FirstOrDefault(item => text.Contains(item, StringComparison.OrdinalIgnoreCase))
                ?? context.ComposeSituation().ProjectName
                ?? context.ListProjects().FirstOrDefault()?.Name;
            var info = string.IsNullOrWhiteSpace(name) ? null : context.ProjectInfo(name);
            return new CommandDispatch
            {
                Kind = CommandKind.Project,
                Title = "Project",
                Body = info?.Format() ?? "No registered project matches that name.",
                HandledWithoutLlm = true
            };
        }

        return Palette(context, text);
    }

    private static CommandDispatch Palette(ICommandContext context, string text)
    {
        var items = context.Palette(text);
        if (items.Count == 0)
        {
            return new CommandDispatch
            {
                Kind = CommandKind.Unknown,
                Title = "Command palette",
                Body = "Nothing ranked yet.",
                HandledWithoutLlm = false
            };
        }

        return new CommandDispatch
        {
            Kind = CommandKind.Palette,
            Title = "What do you want to do?",
            Body = string.Join(Environment.NewLine, items.Take(8).Select(item => item.Title + " — " + item.Subtitle)),
            HandledWithoutLlm = true
        };
    }

    private static bool Looks(string text, params string[] markers) =>
        markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}

public interface ICommandContext
{
    DateTimeOffset Now { get; }

    DetectedIntent DetectIntent(string? utterance = null);

    CurrentSituation ComposeSituation();

    ProjectContinuationContext Continuation();

    DailyBriefingSnapshot Briefing();

    IReadOnlyList<PaletteItem> Palette(string query);

    IReadOnlyList<SearchHit> Search(string query);

    IReadOnlyList<TimelineEntry> Timeline();

    IReadOnlyList<CreativeProject> ListProjects();

    IReadOnlyList<AttentionItem> Attention();

    ProjectIntelligenceSnapshot? ProjectInfo(string name);

    int DefaultFocusMinutes { get; }

    string MemoryCatalog(string? query = null);

    string IntegrationsCatalog();

    string IntegrationPermissions();

    ConnectorOutcome? TryIntegrationRead(string utterance);
}
