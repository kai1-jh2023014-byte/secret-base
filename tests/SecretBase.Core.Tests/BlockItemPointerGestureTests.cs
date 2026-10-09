using SecretBase.Core.Blocks;

namespace SecretBase.Core.Tests;

public class BlockItemPointerGestureTests
{
    [Fact]
    public void TryEnd_ClickWithoutDrag_LaunchesOnce()
    {
        var itemId = Guid.NewGuid();
        var gesture = new BlockItemPointerGesture();
        gesture.Begin(itemId, pressX: 10, pressY: 10);

        var first = gesture.TryEnd();
        var second = gesture.TryEnd();

        Assert.Equal(BlockItemPointerEndKind.Launch, first.Kind);
        Assert.Equal(itemId, first.ItemId);
        Assert.Equal(BlockItemPointerEndKind.Ignored, second.Kind);
    }

    [Fact]
    public void TryEnd_AfterDrag_CommitsDragOnce()
    {
        var itemId = Guid.NewGuid();
        var gesture = new BlockItemPointerGesture();
        gesture.Begin(itemId, pressX: 0, pressY: 0);
        Assert.True(gesture.TryMarkDragging(10, 0));

        var first = gesture.TryEnd();
        var second = gesture.TryEnd();

        Assert.Equal(BlockItemPointerEndKind.CommitDrag, first.Kind);
        Assert.Equal(itemId, first.ItemId);
        Assert.Equal(BlockItemPointerEndKind.Ignored, second.Kind);
    }

    [Fact]
    public void TryMarkDragging_BelowThreshold_DoesNotDrag()
    {
        var gesture = new BlockItemPointerGesture();
        gesture.Begin(Guid.NewGuid(), pressX: 0, pressY: 0);

        Assert.False(gesture.TryMarkDragging(3, 3));
        Assert.False(gesture.IsDragging);

        var end = gesture.TryEnd();
        Assert.Equal(BlockItemPointerEndKind.Launch, end.Kind);
    }

    [Fact]
    public void TryEnd_WhenInactive_IsIgnored()
    {
        var gesture = new BlockItemPointerGesture();
        Assert.Equal(BlockItemPointerEndKind.Ignored, gesture.TryEnd().Kind);
    }

    [Fact]
    public void ReleaseThenCaptureLost_SimulatesSingleLaunch()
    {
        // Mirrors BlockFrame: PointerReleased ends the gesture, then
        // ReleasePointerCapture raises PointerCaptureLost synchronously.
        var itemId = Guid.NewGuid();
        var gesture = new BlockItemPointerGesture();
        gesture.Begin(itemId, 1, 1);

        var launches = 0;
        void OnEnd()
        {
            if (gesture.TryEnd().Kind == BlockItemPointerEndKind.Launch)
            {
                launches++;
            }
        }

        OnEnd();
        OnEnd();

        Assert.Equal(1, launches);
    }

    [Fact]
    public void Cancel_ThenTryEnd_DoesNotLaunch()
    {
        var gesture = new BlockItemPointerGesture();
        gesture.Begin(Guid.NewGuid(), 0, 0);
        gesture.Cancel();

        Assert.False(gesture.IsActive);
        Assert.Equal(BlockItemPointerEndKind.Ignored, gesture.TryEnd().Kind);
    }
}

public class TargetLaunchCoalescerTests
{
    [Fact]
    public void TryAdmit_SameTargetWithinWindow_IsRejected()
    {
        var gate = new TargetLaunchCoalescer(TimeSpan.FromMilliseconds(500));
        var t0 = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        Assert.True(gate.TryAdmit(@"C:\tools\run.bat", t0));
        Assert.False(gate.TryAdmit(@"C:\tools\run.bat", t0.AddMilliseconds(100)));
        Assert.False(gate.TryAdmit(@"C:\TOOLS\run.bat", t0.AddMilliseconds(400)));
    }

    [Fact]
    public void TryAdmit_AfterWindow_AllowsAgain()
    {
        var gate = new TargetLaunchCoalescer(TimeSpan.FromMilliseconds(500));
        var t0 = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        Assert.True(gate.TryAdmit(@"C:\tools\run.bat", t0));
        Assert.True(gate.TryAdmit(@"C:\tools\run.bat", t0.AddMilliseconds(501)));
    }

    [Fact]
    public void TryAdmit_DifferentTarget_IsAllowed()
    {
        var gate = new TargetLaunchCoalescer(TimeSpan.FromMilliseconds(500));
        var t0 = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        Assert.True(gate.TryAdmit(@"C:\a.bat", t0));
        Assert.True(gate.TryAdmit(@"C:\b.bat", t0.AddMilliseconds(10)));
    }

    [Fact]
    public void TryAdmit_BlankTarget_IsRejected()
    {
        var gate = new TargetLaunchCoalescer();
        Assert.False(gate.TryAdmit("  ", DateTimeOffset.UtcNow));
    }
}
