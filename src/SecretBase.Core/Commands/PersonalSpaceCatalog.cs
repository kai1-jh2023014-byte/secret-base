using SecretBase.Core.Automation;
using SecretBase.Core.Memory;

namespace SecretBase.Core.Commands;

/// <summary>Host-facing catalogs for Memory / Automation Permission / Palette execution.</summary>
public static class PersonalSpaceCatalog
{
    public static string Memories(IMemoryStore store, DateTimeOffset now, string? query = null, string? project = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var items = store.RecallRanked(now, query, project, take: 40);
        if (items.Count == 0)
        {
            return "Secret Base is not holding any memories right now.";
        }

        return string.Join(
            Environment.NewLine,
            items.Select(item =>
                $"{item.Kind} · {item.Importance} · {item.Source}"
                + (item.ExpiresAt is null ? " · permanent" : $" · expires {item.ExpiresAt:yyyy-MM-dd}")
                + Environment.NewLine
                + item.Summary
                + (string.IsNullOrWhiteSpace(item.ProjectName) ? string.Empty : "  [" + item.ProjectName + "]")
                + Environment.NewLine
                + "id " + item.Id));
    }

    public static string Rules(IAutomationRuleStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var rules = store.List();
        if (rules.Count == 0)
        {
            return "No automation rules.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            rules.Select(rule =>
                $"{(rule.Enabled ? "On" : "Off")}  {rule.Name}"
                + Environment.NewLine
                + $"Trigger {rule.Trigger} · {rule.Intervention} · confirm={(rule.RequiresConfirmation ? "yes" : "no")}"
                + Environment.NewLine
                + $"Last: {rule.LastRun?.ToString("g") ?? "never"} · {rule.LastResult ?? "—"}"
                + Environment.NewLine
                + "id " + rule.Id));
    }

    public static string UtteranceFor(PaletteItem item) =>
        item.Action switch
        {
            "continue" => "昨日の続きをやりたい",
            "briefing" => "今日何すればいい",
            "focus" => "30分集中したい",
            "capture" => "capture",
            "memory" => "what does secret base remember",
            "timeline" => "今日何してた",
            "explain" => "なぜそう判断したの",
            "cleanup" => "使ってないものを整理したい",
            _ => item.Title
        };
}
