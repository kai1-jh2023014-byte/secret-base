using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Tests;

public class ResponsiveLayoutResolverTests
{
    private static WidgetInstance Widget(double x, double y, double w, double h) =>
        new()
        {
            Type = WidgetTypes.Clock,
            Position = new WidgetPosition(x, y),
            Size = new WidgetSize(w, h)
        };

    private static Block BlockAt(double x, double y, double w, double h) =>
        DefaultBlockFactory.Create("B", x: x, y: y, width: w, height: h);

    [Fact]
    public void Reference_1920x1080_IsIdentity_NoGeometryChange()
    {
        var widgets = new List<WidgetInstance>
        {
            Widget(48, 48, 280, 200),
            Widget(1500, 820, 320, 120)
        };
        var blocks = new List<Block>
        {
            BlockAt(1400, 48, 360, 260),
            BlockAt(1400, 340, 360, 260)
        };

        var snapshot = Snapshot(widgets, blocks);
        var changed = ResponsiveLayoutResolver.AdaptToDisplay(
            widgets,
            blocks,
            fromWidth: DesktopLayoutReference.Width,
            fromHeight: DesktopLayoutReference.Height,
            toWidth: DesktopLayoutReference.Width,
            toHeight: DesktopLayoutReference.Height);

        Assert.False(changed);
        AssertUnchanged(snapshot, widgets, blocks);
    }

    [Fact]
    public void Tall_1920x1200_KeepsWidthAndTop_MovesBottomAnchoredDown()
    {
        // Top clock — should stay.
        var clock = Widget(80, 60, 280, 160);
        // Bottom music-like widget — should keep ~72px above safe bottom (shelf reserve).
        // Safe bottom at 1080: 1080 - 72 - 16 = 992. Music bottom = 900+100=1000 → slightly below
        // Use clearly bottom-anchored: y such that trailing bias applies.
        var music = Widget(1520, 860, 320, 100);
        var widgets = new List<WidgetInstance> { clock, music };

        ResponsiveLayoutResolver.AdaptToDisplay(
            widgets,
            [],
            fromWidth: 1920,
            fromHeight: 1080,
            toWidth: 1920,
            toHeight: 1200);

        Assert.Equal(80, clock.Position.X, 0.5);
        Assert.Equal(60, clock.Position.Y, 0.5);
        Assert.Equal(280, clock.Size.Width, 0.5);
        Assert.Equal(160, clock.Size.Height, 0.5);

        Assert.Equal(1520, music.Position.X, 0.5);
        Assert.Equal(320, music.Size.Width, 0.5);
        Assert.Equal(100, music.Size.Height, 0.5);
        // Extra 120px height → bottom-anchored object shifts down by ~120.
        Assert.InRange(music.Position.Y, 960, 1020);
        Assert.True(music.Position.Y > 860 + 40);
    }

    [Fact]
    public void Tall_1920x1200_DoesNotEnlargeWidgetSizes()
    {
        var block = BlockAt(1400, 200, 360, 260);
        var blocks = new List<Block> { block };

        ResponsiveLayoutResolver.AdaptToDisplay(
            [],
            blocks,
            1920,
            1080,
            1920,
            1200);

        Assert.Equal(360, block.Size.Width, 0.5);
        Assert.Equal(260, block.Size.Height, 0.5);
    }

    [Fact]
    public void Qhd_2560x1440_StaysInsideSafeArea_WithoutUniformStretch()
    {
        var widgets = new List<WidgetInstance>
        {
            Widget(48, 48, 280, 200),
            Widget(1600, 850, 300, 110)
        };
        var blocks = new List<Block> { BlockAt(1500, 80, 360, 400) };

        ResponsiveLayoutResolver.AdaptToDisplay(
            widgets,
            blocks,
            1920,
            1080,
            2560,
            1440);

        var safe = new DesktopDisplayContext(2560, 1440).SafeArea;
        foreach (var w in widgets)
        {
            Assert.InRange(w.Position.X, safe.Left - 0.5, safe.Right - w.Size.Width + 0.5);
            Assert.InRange(w.Position.Y, safe.Top - 0.5, safe.Bottom - w.Size.Height + 0.5);
            // No uniform scale-up: sizes stay at authored values (or shrink only if needed).
            Assert.True(w.Size.Width <= 300 + 0.5);
            Assert.True(w.Size.Height <= 200 + 0.5);
        }

        foreach (var b in blocks)
        {
            Assert.InRange(b.Position.X, safe.Left - 0.5, safe.Right - b.Size.Width + 0.5);
            Assert.InRange(b.Position.Y, safe.Top - 0.5, safe.Bottom - b.Size.Height + 0.5);
        }
    }

    [Fact]
    public void Small_1366x768_ClampsWithoutCrash()
    {
        var widgets = new List<WidgetInstance>
        {
            Widget(1600, 900, 400, 300),
            Widget(48, 48, 280, 200)
        };
        var blocks = new List<Block> { BlockAt(1400, 40, 500, 400) };

        ResponsiveLayoutResolver.AdaptToDisplay(
            widgets,
            blocks,
            1920,
            1080,
            1366,
            768);

        var safe = new DesktopDisplayContext(1366, 768).SafeArea;
        foreach (var w in widgets)
        {
            Assert.True(w.Position.X + w.Size.Width <= safe.Right + 0.5);
            Assert.True(w.Position.Y + w.Size.Height <= safe.Bottom + 0.5);
            Assert.True(w.Position.X >= safe.Left - 0.5);
            Assert.True(w.Position.Y >= safe.Top - 0.5);
        }

        foreach (var b in blocks)
        {
            Assert.True(b.Size.Width <= safe.Width + 0.5);
            Assert.True(b.Position.X + b.Size.Width <= safe.Right + 0.5);
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void DpiScale_DoesNotAlterDipGeometry(double dpiScale)
    {
        var widgets = new List<WidgetInstance> { Widget(100, 100, 200, 120) };
        var from = new DesktopDisplayContext(1920, 1080, dpiScale);
        var to = new DesktopDisplayContext(1920, 1080, dpiScale);

        var changed = ResponsiveLayoutResolver.AdaptToDisplay(widgets, [], from, to);

        Assert.False(changed);
        Assert.Equal(100, widgets[0].Position.X, 0.5);
        Assert.Equal(100, widgets[0].Position.Y, 0.5);
        Assert.Equal(200, widgets[0].Size.Width, 0.5);
    }

    [Fact]
    public void Resize_1080_to_1200_to_1080_RestoresBottomAnchor()
    {
        var music = Widget(1520, 860, 320, 100);
        var widgets = new List<WidgetInstance> { music };
        var originalY = music.Position.Y;

        ResponsiveLayoutResolver.AdaptToDisplay(widgets, [], 1920, 1080, 1920, 1200);
        Assert.True(music.Position.Y > originalY);

        ResponsiveLayoutResolver.AdaptToDisplay(widgets, [], 1920, 1200, 1920, 1080);
        Assert.Equal(originalY, music.Position.Y, 1.0);
        Assert.Equal(1520, music.Position.X, 0.5);
    }

    [Fact]
    public void InferAnchor_BottomRight_ForCornerObject()
    {
        var safe = DesktopDisplayContext.Reference().SafeArea;
        var pos = new WidgetPosition(safe.Right - 320 - 20, safe.Bottom - 100 - 20);
        var size = new WidgetSize(320, 100);
        Assert.Equal(LayoutAnchor.BottomRight, ResponsiveLayoutResolver.InferAnchor(pos, size, safe));
    }

    [Fact]
    public void InferAnchor_TopLeft_ForHeaderObject()
    {
        var safe = DesktopDisplayContext.Reference().SafeArea;
        var pos = new WidgetPosition(safe.Left + 10, safe.Top + 10);
        var size = new WidgetSize(280, 160);
        Assert.Equal(LayoutAnchor.TopLeft, ResponsiveLayoutResolver.InferAnchor(pos, size, safe));
    }

    [Fact]
    public void MapAxis_SameUsable_KeepsLeading()
    {
        var mapped = ResponsiveLayoutResolver.MapAxis(
            leading: 40,
            trailing: 200,
            size: 100,
            fromUsable: 900,
            toUsable: 900,
            toOrigin: 16);
        Assert.Equal(56, mapped, 0.01);
    }

    [Fact]
    public void DesktopLayout_CreateDefault_BindsReferenceViewport()
    {
        var layout = DesktopLayout.CreateDefault();
        Assert.Equal(3, layout.SchemaVersion);
        Assert.Equal(DesktopLayoutReference.Width, layout.ReferenceWidth);
        Assert.Equal(DesktopLayoutReference.Height, layout.ReferenceHeight);
        Assert.Equal(DesktopLayoutReference.Width, layout.LayoutViewportWidth);
        Assert.Equal(DesktopLayoutReference.Height, layout.LayoutViewportHeight);
    }

    [Fact]
    public void Tall_DoesNotCollideWithBottomReserve()
    {
        var music = Widget(1520, 860, 320, 100);
        ResponsiveLayoutResolver.AdaptToDisplay(
            new[] { music },
            [],
            1920,
            1080,
            1920,
            1200);

        var bottomLimit = 1200 - DesktopLayoutReference.BottomReserve - DesktopLayoutReference.Margin;
        Assert.True(music.Position.Y + music.Size.Height <= bottomLimit + 0.5);
    }

    private static List<(double X, double Y, double W, double H)> Snapshot(
        IReadOnlyList<WidgetInstance> widgets,
        IReadOnlyList<Block> blocks)
    {
        var list = new List<(double, double, double, double)>();
        foreach (var w in widgets)
        {
            list.Add((w.Position.X, w.Position.Y, w.Size.Width, w.Size.Height));
        }

        foreach (var b in blocks)
        {
            list.Add((b.Position.X, b.Position.Y, b.Size.Width, b.Size.Height));
        }

        return list;
    }

    private static void AssertUnchanged(
        List<(double X, double Y, double W, double H)> snapshot,
        IReadOnlyList<WidgetInstance> widgets,
        IReadOnlyList<Block> blocks)
    {
        var i = 0;
        foreach (var w in widgets)
        {
            Assert.Equal(snapshot[i].X, w.Position.X, 0.5);
            Assert.Equal(snapshot[i].Y, w.Position.Y, 0.5);
            Assert.Equal(snapshot[i].W, w.Size.Width, 0.5);
            Assert.Equal(snapshot[i].H, w.Size.Height, 0.5);
            i++;
        }

        foreach (var b in blocks)
        {
            Assert.Equal(snapshot[i].X, b.Position.X, 0.5);
            Assert.Equal(snapshot[i].Y, b.Position.Y, 0.5);
            Assert.Equal(snapshot[i].W, b.Size.Width, 0.5);
            Assert.Equal(snapshot[i].H, b.Size.Height, 0.5);
            i++;
        }
    }
}
