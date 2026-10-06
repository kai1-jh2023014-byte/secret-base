using SecretBase.Core.Desktop;

namespace SecretBase.Core.Tests;

public class DesktopDisplayDiagnosticsReportTests
{
    [Fact]
    public void Format_IncludesRequiredSections()
    {
        var report = new DesktopDisplayDiagnosticsReport
        {
            Reason = "work-area-changed-after-overlay",
            Sequence = 3,
            DisplayId = "disp-1",
            DisplayBounds = "0,0 1920×1200",
            DisplayWorkArea = "0,0 1920×1160",
            WorkAreaWidthPx = 1920,
            WorkAreaHeightPx = 1160,
            BoundsWidthPx = 1920,
            BoundsHeightPx = 1200,
            WindowWidthPx = 1920,
            WindowHeightPx = 1160,
            PageActualWidthDip = 1536,
            PageActualHeightDip = 928,
            RasterizationScale = 1.25,
            PhysicalWidthFromDip = 1920,
            PhysicalHeightFromDip = 1160,
            AuthoredViewportWidth = 1920,
            AuthoredViewportHeight = 1080,
            ResolverInputWidth = 1920,
            ResolverInputHeight = 1080,
            ResolverOutputWidth = 1536,
            ResolverOutputHeight = 928,
            ResolverIdentity = false,
            SafeArea = "L=16 T=16 R=1520 B=840",
            Objects =
            [
                new DesktopObjectDiagnosticsRow
                {
                    Kind = "block",
                    Label = "開発品",
                    SavedX = 1200,
                    SavedY = 560,
                    SavedW = 700,
                    SavedH = 220,
                    ResolvedX = 1200,
                    ResolvedY = 620,
                    ResolvedW = 700,
                    ResolvedH = 220,
                    CanvasX = 1200,
                    CanvasY = 620,
                    CanvasW = 700,
                    CanvasH = 220
                }
            ]
        };

        var text = report.Format();
        Assert.Contains("=== DISPLAY CHANGE ===", text);
        Assert.Contains("Reason: work-area-changed-after-overlay", text);
        Assert.Contains("WorkArea px: 1920 × 1160", text);
        Assert.Contains("RasterizationScale: 1.25", text);
        Assert.Contains("Reference: 1920 × 1080", text);
        Assert.Contains("開発品", text);
        Assert.Contains("SavedGeometryMutated: False", text);
        Assert.Contains("=======================", text);

        var compact = report.FormatCompactPanel();
        Assert.Contains("Scale: 1.25", compact);
        Assert.Contains("開発品", compact);
    }
}
