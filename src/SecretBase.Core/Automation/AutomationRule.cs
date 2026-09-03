using SecretBase.Core.Intent;

namespace SecretBase.Core.Automation;

public sealed class AutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public AutomationTriggerKind Trigger { get; set; } = AutomationTriggerKind.Time;

    public DetectedIntentKind? RequiredIntent { get; set; }

    public InterventionMode Intervention { get; set; } = InterventionMode.Suggest;

    public bool RequiresConfirmation { get; set; } = true;

    public string Action { get; set; } = "suggest-continue";

    public int CooldownMinutes { get; set; } = 30;

    public DateTimeOffset? LastRun { get; set; }

    public string? LastResult { get; set; }
}

public sealed class AutomationRuleDocument
{
    public const int SchemaVersion = 1;

    public int Schema { get; set; } = SchemaVersion;

    public List<AutomationRule> Rules { get; set; } = [];
}

public interface IAutomationRuleStore
{
    IReadOnlyList<AutomationRule> List();

    void Save(AutomationRule rule);

    void Remove(string id);

    void ReplaceAll(IEnumerable<AutomationRule> rules);
}

public sealed class AutomationRuleStore : IAutomationRuleStore
{
    private readonly object _gate = new();
    private readonly List<AutomationRule> _rules = [];

    public AutomationRuleStore(IEnumerable<AutomationRule>? seed = null)
    {
        if (seed is not null)
        {
            _rules.AddRange(seed);
        }
        else
        {
            _rules.AddRange(DefaultRules());
        }
    }

    public IReadOnlyList<AutomationRule> List()
    {
        lock (_gate)
        {
            return _rules.ToList();
        }
    }

    public void Save(AutomationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        lock (_gate)
        {
            var index = _rules.FindIndex(item => item.Id == rule.Id);
            if (index >= 0)
            {
                _rules[index] = rule;
            }
            else
            {
                _rules.Add(rule);
            }
        }
    }

    public void Remove(string id)
    {
        lock (_gate)
        {
            _rules.RemoveAll(item => item.Id == id);
        }
    }

    public void ReplaceAll(IEnumerable<AutomationRule> rules)
    {
        lock (_gate)
        {
            _rules.Clear();
            _rules.AddRange(rules);
            if (_rules.Count == 0)
            {
                _rules.AddRange(DefaultRules());
            }
        }
    }

    public static IReadOnlyList<AutomationRule> DefaultRules() =>
    [
        new()
        {
            Id = "startup-continue",
            Name = "When Secret Base opens, offer Continue if a previous session exists",
            Trigger = AutomationTriggerKind.Startup,
            RequiredIntent = DetectedIntentKind.ResumePreviousSession,
            Action = "suggest-continue",
            CooldownMinutes = 120
        },
        new()
        {
            Id = "calendar-continue",
            Name = "When a work calendar block starts, suggest Continue",
            Trigger = AutomationTriggerKind.Calendar,
            RequiredIntent = DetectedIntentKind.ContinueProject,
            Action = "suggest-continue",
            CooldownMinutes = 45
        },
        new()
        {
            Id = "evening-focus",
            Name = "Around typical work hours, suggest a focus session",
            Trigger = AutomationTriggerKind.Time,
            RequiredIntent = DetectedIntentKind.StartFocus,
            Action = "suggest-focus",
            Intervention = InterventionMode.Suggest,
            RequiresConfirmation = false,
            CooldownMinutes = 180
        }
    ];
}

/// <summary>Event-driven rule evaluation. No always-on polling loop.</summary>
public static class AutomationScheduler
{
    public static AutomationRule? Match(
        IReadOnlyList<AutomationRule> rules,
        AutomationTriggerKind trigger,
        DetectedIntent intent,
        DateTimeOffset now)
    {
        rules ??= [];
        return rules.FirstOrDefault(rule =>
            rule.Enabled
            && rule.Trigger == trigger
            && (rule.RequiredIntent is null || rule.RequiredIntent == intent.Kind)
            && (rule.LastRun is null || now - rule.LastRun.Value >= TimeSpan.FromMinutes(Math.Max(5, rule.CooldownMinutes))));
    }
}
