using SecretBase.Platform.Abstractions;

namespace SecretBase.Infrastructure.Tests;

public class DesktopOverlayPlacementTests
{
    [Fact]
    public void AboveDesktop_InsertsAfterTheWindowAlreadyAboveTheShell()
    {
        var placement = DesktopOverlayPlacement.AboveDesktop(
            shellWindow: 100,
            windowAboveShell: 200,
            ourWindow: 300,
            windowAboveShellIsTopMost: false);

        Assert.True(placement.Change);
        Assert.Equal(200, placement.InsertAfter);
    }

    [Fact]
    public void AboveDesktop_DoesNotMoveWhenAlreadyDirectlyAboveTheShell()
    {
        var placement = DesktopOverlayPlacement.AboveDesktop(
            shellWindow: 100,
            windowAboveShell: 300,
            ourWindow: 300,
            windowAboveShellIsTopMost: false);

        Assert.False(placement.Change);
    }

    [Fact]
    public void AboveDesktop_DoesNotInsertAfterATopMostWindow()
    {
        var placement = DesktopOverlayPlacement.AboveDesktop(
            shellWindow: 100,
            windowAboveShell: 200,
            ourWindow: 300,
            windowAboveShellIsTopMost: true);

        Assert.False(placement.Change);
    }

    [Fact]
    public void AboveDesktop_UsesHwndTopWhenTheShellIsAlreadyInFront()
    {
        var placement = DesktopOverlayPlacement.AboveDesktop(
            shellWindow: 100,
            windowAboveShell: 0,
            ourWindow: 300,
            windowAboveShellIsTopMost: false);

        Assert.True(placement.Change);
        Assert.Equal(DesktopOverlayPlacement.HwndTop, placement.InsertAfter);
    }

    [Fact]
    public void AboveDesktop_FallsBackToHwndBottomWhenTheShellWindowIsMissing()
    {
        var placement = DesktopOverlayPlacement.AboveDesktop(
            shellWindow: 0,
            windowAboveShell: 0,
            ourWindow: 300,
            windowAboveShellIsTopMost: false);

        Assert.True(placement.Change);
        Assert.Equal(DesktopOverlayPlacement.HwndBottom, placement.InsertAfter);
    }
}
