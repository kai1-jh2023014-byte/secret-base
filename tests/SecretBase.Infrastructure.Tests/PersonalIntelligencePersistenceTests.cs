using SecretBase.Core.Activity;
using SecretBase.Core.Automation;
using SecretBase.Core.Intent;
using SecretBase.Core.Memory;
using SecretBase.Infrastructure.Persistence;

namespace SecretBase.Infrastructure.Tests;

public class PersonalIntelligencePersistenceTests
{
    [Fact]
    public void Memory_RoundTrips_AndCorruptBecomesEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "memory.json");
            var store = new JsonMemoryStore(path);
            store.Remember(new MemoryEntry
            {
                Key = "last-session",
                Summary = "Windows AutoStart QA",
                Scope = MemoryScope.Session
            });
            var restored = new JsonMemoryStore(path).Recall(DateTimeOffset.UtcNow, query: "AutoStart");
            Assert.Single(restored);

            File.WriteAllText(path, "{ broken");
            Assert.Empty(new JsonMemoryStore(path).Recall(DateTimeOffset.UtcNow));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ActivityAndFeedback_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var activityPath = Path.Combine(dir, "activity.json");
            var activity = new JsonActivityStore(activityPath);
            activity.Record(new ActivityEvent
            {
                Title = "Workspace prepared",
                Kind = ActivityKind.WorkspacePrepared
            });
            Assert.Single(new JsonActivityStore(activityPath).Recent());

            var feedbackPath = Path.Combine(dir, "feedback.json");
            var feedback = new JsonAutomationFeedbackStore(feedbackPath);
            feedback.Record(new AutomationFeedback
            {
                Intent = DetectedIntentKind.ContinueProject,
                Accepted = true
            });
            Assert.Equal(1, new JsonAutomationFeedbackStore(feedbackPath)
                .AcceptanceRate(DetectedIntentKind.ContinueProject));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Sessions_RoundTrip_AndCorruptBecomesEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "sessions.json");
            var store = new JsonWorkSessionStore(path);
            var now = DateTimeOffset.UtcNow;
            store.StartOrContinue(now, "p1", "Secret Base", "Base AI");
            store.Touch("Intent Engine", "IntentEngine.cs", "Evidence ranking", false);
            var restored = new JsonWorkSessionStore(path);
            Assert.Equal("Secret Base", restored.Current?.ProjectName);
            Assert.Contains("Intent Engine", restored.Current?.Summary, StringComparison.Ordinal);

            File.WriteAllText(path, "{ broken");
            Assert.Null(new JsonWorkSessionStore(path).Current);
            Assert.Empty(new JsonWorkSessionStore(path).Recent());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Memory_UpdateMerge_Persists_AndCorruptRecovers()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "memory.json");
            var store = new JsonMemoryStore(path);
            var saved = store.Remember(new MemoryEntry { Key = "note", Summary = "one" });
            store.Update(saved.Id, summary: "two");
            store.Merge(new MemoryEntry { Key = "note", Summary = "three" });
            Assert.Equal("three", new JsonMemoryStore(path).Recall(DateTimeOffset.UtcNow, query: "three")[0].Summary);

            File.WriteAllText(path, "{ broken");
            Assert.Empty(new JsonMemoryStore(path).Recall(DateTimeOffset.UtcNow));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AutomationRules_RoundTrip_AndCorruptBecomesDefaults()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secret-base-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "automation-rules.json");
            var store = new JsonAutomationRuleStore(path);
            var rule = store.List()[0];
            rule.Enabled = false;
            store.Save(rule);
            Assert.False(new JsonAutomationRuleStore(path).List().First(item => item.Id == rule.Id).Enabled);

            File.WriteAllText(path, "{ broken");
            Assert.NotEmpty(new JsonAutomationRuleStore(path).List());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
