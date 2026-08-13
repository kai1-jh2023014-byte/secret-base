using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Tests;

public class DesktopArrangeTests
{
    [Fact]
    public void DesktopWidgetLayout_ArrangeEvenly_PlacesTwoInRow()
    {
        var widgets = new List<WidgetInstance>
        {
            new()
            {
                Type = WidgetTypes.Clock,
                Position = new WidgetPosition(900, 400),
                Size = new WidgetSize(200, 120)
            },
            new()
            {
                Type = WidgetTypes.Text,
                Position = new WidgetPosition(50, 500),
                Size = new WidgetSize(200, 120)
            }
        };

        DesktopWidgetLayout.ArrangeEvenly(widgets, areaWidth: 800, areaHeight: 600, margin: 24, gap: 24);

        Assert.True(widgets[0].Position.X < widgets[1].Position.X);
        Assert.Equal(widgets[0].Position.Y, widgets[1].Position.Y, 0.01);
        Assert.True(widgets[0].Position.X >= 24);
        Assert.True(widgets[0].Position.Y >= 24);
    }

    [Fact]
    public void DesktopWidgetLayout_ArrangeEvenly_UsesMultipleRows()
    {
        var widgets = Enumerable.Range(0, 4)
            .Select(i => new WidgetInstance
            {
                Type = WidgetTypes.Text,
                Position = new WidgetPosition(i * 10, i * 10),
                Size = new WidgetSize(280, 160)
            })
            .ToList();

        // Usable width fits two cells of 280 + gap 24.
        DesktopWidgetLayout.ArrangeEvenly(widgets, areaWidth: 640, areaHeight: 800, margin: 24, gap: 24);

        Assert.Equal(widgets[0].Position.Y, widgets[1].Position.Y, 0.01);
        Assert.True(widgets[2].Position.Y > widgets[0].Position.Y);
        Assert.Equal(widgets[2].Position.Y, widgets[3].Position.Y, 0.01);
    }

    [Fact]
    public void DesktopWidgetLayout_ArrangeEvenly_Empty_NoOp()
    {
        DesktopWidgetLayout.ArrangeEvenly([], 800, 600);
    }

    [Fact]
    public void DesktopBlockLayout_ArrangeEvenly_PlacesTwoInRow()
    {
        var blocks = new List<Block>
        {
            DefaultBlockFactory.Create("A", x: 900, y: 10, width: 260, height: 180),
            DefaultBlockFactory.Create("B", x: 20, y: 400, width: 260, height: 180)
        };

        DesktopBlockLayout.ArrangeEvenly(blocks, areaWidth: 900, areaHeight: 700, margin: 24, gap: 28);

        Assert.True(blocks[0].Position.X < blocks[1].Position.X);
        Assert.Equal(blocks[0].Position.Y, blocks[1].Position.Y, 0.01);
    }

    [Fact]
    public void DesktopBlockLayout_ArrangeEvenlyBelow_OffsetsUnderWidgetBand()
    {
        var blocks = new List<Block>
        {
            DefaultBlockFactory.Create("A", x: 0, y: 0, width: 260, height: 180),
            DefaultBlockFactory.Create("B", x: 0, y: 0, width: 260, height: 180)
        };

        const double topOffset = 220;
        DesktopBlockLayout.ArrangeEvenlyBelow(
            blocks,
            areaWidth: 900,
            areaHeight: 900,
            topOffset: topOffset,
            margin: 24,
            gap: 28);

        Assert.True(blocks[0].Position.Y >= topOffset);
        Assert.Equal(blocks[0].Position.Y, blocks[1].Position.Y, 0.01);
        Assert.True(blocks[0].Position.X < blocks[1].Position.X);
    }
}
