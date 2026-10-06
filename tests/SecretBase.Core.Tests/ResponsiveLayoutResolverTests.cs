using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Tests;

public class ResponsiveLayoutResolverTests
{
    private static WidgetInstance Widget(Guid id, double x, double y, double w, double h) =>
        new()
        {
            Id = id,
            Type = WidgetTypes.Clock,
            Position = new WidgetPosition(x, y),
            Size = new WidgetSize(w, h)
        };

    private static Block BlockAt(Guid id, double x, double y, double w, double h)
    {
        var block = DefaultBlockFactory.Create("B", x: x, y: y, width: w, height: h);
        block.Id = id;
        return block;
    }

    private static (List<WidgetInstance> Widgets, List<Block> Blocks) SampleDesk()
    {
        var clockId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var pomoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var musicId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var aiId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var productsId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var widgets = new List<WidgetInstance>
        {
            Widget(clockId, 420, 280, 360, 200),
            Widget(pomoId, 1180, 820, 200, 140),
            Widget(musicId, 1400, 820, 420, 140)
        };
        var blocks = new List<Block>
        {
            BlockAt(aiId, 1180, 48, 340, 240),
            BlockAt(productsId, 1180, 560, 700, 220)
        };
        return (widgets, blocks);
    }

    private static List<(Guid Id, double X, double Y, double W, double H)> Snapshot(
        IReadOnlyList<WidgetInstance> widgets,
        IReadOnlyList<Block> blocks)
    {
        var list = new List<(Guid, double, double, double, double)>();
        foreach (var w in widgets)
        {
            list.Add((w.Id, w.Position.X, w.Position.Y, w.Size.Width, w.Size.Height));
        }

        foreach (var b in blocks)
        {
            list.Add((b.Id, b.Position.X, b.Position.Y, b.Size.Width, b.Size.Height));
        }

        return list;
    }

    private static void AssertSavedUnchanged(
        List<(Guid Id, double X, double Y, double W, double H)> before,
        IReadOnlyList<WidgetInstance> widgets,
        IReadOnlyList<Block> blocks)
    {
        var after = Snapshot(widgets, blocks);
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].Id, after[i].Id);
            Assert.Equal(before[i].X, after[i].X, 0.01);
            Assert.Equal(before[i].Y, after[i].Y, 0.01);
            Assert.Equal(before[i].W, after[i].W, 0.01);
            Assert.Equal(before[i].H, after[i].H, 0.01);
        }
    }

    [Fact]
    public void TestA_Reference_1920x1080_MatchesSaved()
    {
        var (widgets, blocks) = SampleDesk();
        var saved = Snapshot(widgets, blocks);

        var resolved = ResponsiveLayoutResolver.Resolve(
            widgets,
            blocks,
            1920,
            1080,
            1920,
            1080);

        Assert.True(resolved.IsIdentity);
        AssertSavedUnchanged(saved, widgets, blocks);
        foreach (var w in widgets)
        {
            var r = resolved.Widgets[w.Id];
            Assert.Equal(w.Position.X, r.X, 0.5);
            Assert.Equal(w.Position.Y, r.Y, 0.5);
            Assert.Equal(w.Size.Width, r.Width, 0.5);
            Assert.Equal(w.Size.Height, r.Height, 0.5);
        }
    }

    [Fact]
    public void TestB_RoundTrip_1080_1200_1080_RestoresSaved()
    {
        var (widgets, blocks) = SampleDesk();
        var original = Snapshot(widgets, blocks);

        var tall = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1200);
        AssertSavedUnchanged(original, widgets, blocks);

        // Simulate "display" then return — resolve again from the same saved input.
        var back = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1080);
        AssertSavedUnchanged(original, widgets, blocks);
        foreach (var entry in original)
        {
            if (tall.Widgets.ContainsKey(entry.Id))
            {
                var r = back.Widgets[entry.Id];
                Assert.Equal(entry.X, r.X, 0.5);
                Assert.Equal(entry.Y, r.Y, 0.5);
            }
            else
            {
                var r = back.Blocks[entry.Id];
                Assert.Equal(entry.X, r.X, 0.5);
                Assert.Equal(entry.Y, r.Y, 0.5);
            }
        }
    }

    [Fact]
    public void TestC_MultipleRoundTrips_NoCumulativeDrift()
    {
        var (widgets, blocks) = SampleDesk();
        var original = Snapshot(widgets, blocks);

        for (var i = 0; i < 5; i++)
        {
            _ = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1200);
            var back = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1080);
            AssertSavedUnchanged(original, widgets, blocks);
            foreach (var entry in original)
            {
                var r = back.Widgets.TryGetValue(entry.Id, out var wr) ? wr : back.Blocks[entry.Id];
                Assert.Equal(entry.X, r.X, 0.5);
                Assert.Equal(entry.Y, r.Y, 0.5);
                Assert.Equal(entry.W, r.Width, 0.5);
                Assert.Equal(entry.H, r.Height, 0.5);
            }
        }
    }

    [Fact]
    public void TestD_Resolve_DoesNotMutateInputReference()
    {
        var (widgets, blocks) = SampleDesk();
        var before = Snapshot(widgets, blocks);

        for (var i = 0; i < 10; i++)
        {
            _ = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1200);
            _ = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 2560, 1440);
            _ = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1366, 768);
        }

        AssertSavedUnchanged(before, widgets, blocks);
    }

    [Fact]
    public void TestE_UserEdit_BecomesNewAuthoredLayout()
    {
        var (widgets, blocks) = SampleDesk();
        // User drags music on a 1200 display — saved coords + authored viewport update.
        var music = widgets[2];
        music.Position.X = 1450;
        music.Position.Y = 980;
        const double authoredW = 1920;
        const double authoredH = 1200;

        var resolvedOn1080 = ResponsiveLayoutResolver.Resolve(
            widgets,
            blocks,
            authoredW,
            authoredH,
            1920,
            1080);

        // Bottom-anchored music should keep trailing gap when shrinking height.
        Assert.True(resolvedOn1080.Widgets[music.Id].Y < music.Position.Y);
        Assert.Equal(1450, resolvedOn1080.Widgets[music.Id].X, 0.5);
        // Saved user edit untouched.
        Assert.Equal(1450, music.Position.X, 0.01);
        Assert.Equal(980, music.Position.Y, 0.01);
    }

    [Fact]
    public void TestF_Resolve_IsDisplayOnly_NoSaveSideEffects()
    {
        var (widgets, blocks) = SampleDesk();
        var before = Snapshot(widgets, blocks);
        var layout = new DesktopLayout
        {
            LayoutViewportWidth = 1920,
            LayoutViewportHeight = 1080,
            Widgets = widgets,
            Blocks = blocks
        };

        _ = ResponsiveLayoutResolver.Resolve(layout.Widgets, layout.Blocks, 1920, 1080, 1920, 1200);

        Assert.Equal(1920, layout.LayoutViewportWidth);
        Assert.Equal(1080, layout.LayoutViewportHeight);
        AssertSavedUnchanged(before, layout.Widgets, layout.Blocks);
    }

    [Fact]
    public void Tall_1920x1200_KeepsX_AndDoesNotEnlarge()
    {
        var (widgets, blocks) = SampleDesk();
        var resolved = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1920, 1200);

        foreach (var w in widgets)
        {
            var r = resolved.Widgets[w.Id];
            Assert.Equal(w.Position.X, r.X, 0.5);
            Assert.Equal(w.Size.Width, r.Width, 0.5);
            Assert.Equal(w.Size.Height, r.Height, 0.5);
        }

        foreach (var b in blocks)
        {
            var r = resolved.Blocks[b.Id];
            Assert.Equal(b.Position.X, r.X, 0.5);
            Assert.Equal(b.Size.Width, r.Width, 0.5);
            Assert.Equal(b.Size.Height, r.Height, 0.5);
        }
    }

    [Fact]
    public void Tall_BottomAnchored_MovesDown_TopStays()
    {
        var topId = Guid.NewGuid();
        var bottomId = Guid.NewGuid();
        var widgets = new List<WidgetInstance>
        {
            Widget(topId, 80, 60, 280, 160),
            Widget(bottomId, 1520, 860, 320, 100)
        };

        var resolved = ResponsiveLayoutResolver.Resolve(widgets, [], 1920, 1080, 1920, 1200);

        Assert.Equal(60, resolved.Widgets[topId].Y, 0.5);
        Assert.InRange(resolved.Widgets[bottomId].Y, 960, 1020);
        Assert.True(resolved.Widgets[bottomId].Y > 860 + 40);
    }

    [Fact]
    public void Tall_Resolved_DoesNotOverlap_WhenStacked()
    {
        var upperId = Guid.NewGuid();
        var lowerId = Guid.NewGuid();
        var blocks = new List<Block>
        {
            BlockAt(upperId, 1200, 500, 600, 220),
            BlockAt(lowerId, 1200, 700, 600, 220)
        };

        var resolved = ResponsiveLayoutResolver.Resolve([], blocks, 1920, 1080, 1920, 1200);
        var a = resolved.Blocks[upperId];
        var b = resolved.Blocks[lowerId];
        var overlaps = a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
        Assert.False(overlaps);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    public void DpiScale_DoesNotAlterDipGeometry(double dpiScale)
    {
        var (widgets, blocks) = SampleDesk();
        var from = new DesktopDisplayContext(1920, 1080, dpiScale);
        var to = new DesktopDisplayContext(1920, 1080, dpiScale);
        var resolved = ResponsiveLayoutResolver.Resolve(widgets, blocks, from, to);
        Assert.True(resolved.IsIdentity);
        Assert.Equal(widgets[0].Position.X, resolved.Widgets[widgets[0].Id].X, 0.5);
    }

    [Fact]
    public void Small_1366x768_ClampsInsideSafeArea()
    {
        var (widgets, blocks) = SampleDesk();
        var resolved = ResponsiveLayoutResolver.Resolve(widgets, blocks, 1920, 1080, 1366, 768);
        var safe = new DesktopDisplayContext(1366, 768).SafeArea;
        foreach (var rect in resolved.Widgets.Values.Concat(resolved.Blocks.Values))
        {
            Assert.True(rect.X >= safe.Left - 0.5);
            Assert.True(rect.Y >= safe.Top - 0.5);
            Assert.True(rect.Right <= safe.Right + 0.5);
            Assert.True(rect.Bottom <= safe.Bottom + 0.5);
        }
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
    public void MapAxis_SameUsable_KeepsLeading()
    {
        var mapped = ResponsiveLayoutResolver.MapAxis(40, 200, 100, 900, 900, 16);
        Assert.Equal(56, mapped, 0.01);
    }
}
