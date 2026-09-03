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
}
