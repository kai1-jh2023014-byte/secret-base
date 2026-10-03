using SecretBase.Infrastructure.Logging;

namespace SecretBase.Infrastructure.Tests;

public class StartupTraceTests
{
    [Fact]
    public void Write_RoundTripsStatusWithUtf8Bom()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secretbase-trace-" + Guid.NewGuid().ToString("N"));
        try
        {
            StartupTrace.Write(dir, StartupTrace.VisibleStatus, "widgets above the desktop");
            var path = Path.Combine(dir, StartupTrace.FileName);
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
            Assert.Equal(StartupTrace.VisibleStatus, StartupTrace.ReadStatus(dir));
            Assert.Contains("widgets above the desktop", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    [Fact]
    public void ReadStatus_MissingFileReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secretbase-trace-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Null(StartupTrace.ReadStatus(dir));
    }
}
