using SecretBase.Core.Progress;
using SecretBase.Infrastructure.Progress;

namespace SecretBase.Infrastructure.Tests;

public class ProgressGenesisHistoryStoreTests
{
    [Fact]
    public void AppendIfChanged_KeepsLatestAndSkipsDuplicates()
    {
        var dir = CreateTempDir();
        try
        {
            var path = Path.Combine(dir, "progress-genesis-log.json");
            var store = new JsonProgressGenesisHistoryStore(path);

            Assert.Null(store.LoadLatest());

            var first = new ProgressGenesisSnapshot
            {
                Progress = new ProgressTrack { Percent = 0, Status = "Professional Readiness 0%" },
                Genesis = new GenesisTrack { Percent = 0, Phase = "MusicLab", Status = "ready" },
                SourceKind = ProgressGenesisSourceKinds.PersonalSystems
            };
            Assert.True(store.AppendIfChanged(first));
            Assert.False(store.AppendIfChanged(first));

            var latest = store.LoadLatest();
            Assert.NotNull(latest);
            Assert.Equal(0, latest!.Progress.Percent);

            var updated = new ProgressGenesisSnapshot
            {
                Progress = new ProgressTrack { Percent = 18, Status = "Professional Readiness 18%" },
                Genesis = new GenesisTrack { Percent = 0, Phase = "MusicLab", Status = "ready" },
                SourceKind = ProgressGenesisSourceKinds.PersonalSystems
            };
            Assert.True(store.AppendIfChanged(updated));
            Assert.Equal(18, store.LoadLatest()!.Progress.Percent);

            var doc = store.LoadOrCreate();
            Assert.Equal(2, doc.Entries.Count);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sb-progress-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
