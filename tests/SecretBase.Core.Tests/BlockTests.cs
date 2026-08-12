using SecretBase.Core.Blocks;
using SecretBase.Core.Desktop;
using SecretBase.Core.Widgets;

namespace SecretBase.Core.Tests;

public class BlockModelTests
{
    [Fact]
    public void DefaultBlockFactory_CreatesNamedEmptyBlock()
    {
        var block = DefaultBlockFactory.Create("DEVELOPMENT", x: 100, y: 80, width: 400, height: 300);

        Assert.Equal("DEVELOPMENT", block.Name);
        Assert.Equal(100, block.Position.X);
        Assert.Equal(80, block.Position.Y);
        Assert.Equal(400, block.Size.Width);
        Assert.Equal(300, block.Size.Height);
        Assert.Empty(block.Items);
        Assert.Null(block.Theme);
        Assert.NotEqual(Guid.Empty, block.Id);
    }

    [Fact]
    public void DefaultBlockFactory_BlankName_UsesFallback()
    {
        var block = DefaultBlockFactory.Create("   ");
        Assert.Equal("New Block", block.Name);
    }

    [Fact]
    public void Block_ClampSize_EnforcesMinimum()
    {
        var block = DefaultBlockFactory.Create("Tiny", width: 10, height: 10);
        block.ClampSize();
        Assert.Equal(Block.MinWidth, block.Size.Width);
        Assert.Equal(Block.MinHeight, block.Size.Height);
    }

    [Fact]
    public void BlockItem_EnsurePlacement_AssignsGridSlot()
    {
        var item = new BlockItem { Name = "A", Target = @"C:\a.exe" };
        Assert.False(item.HasPlacement);
        item.EnsurePlacement(4);
        Assert.True(item.HasPlacement);
        Assert.Equal(8 + (4 % 3) * (BlockItem.TileWidth + 8), item.X);
        Assert.Equal(8 + (4 / 3) * (BlockItem.TileHeight + 8), item.Y);
    }

    [Fact]
    public void BlockItem_ClampPlacement_KeepsInsideBounds()
    {
        var item = new BlockItem { X = 5000, Y = 5000 };
        item.ClampPlacement(200, 180);
        Assert.True(item.X <= 200 - BlockItem.TileWidth);
        Assert.True(item.Y <= 180 - BlockItem.TileHeight);
    }

    [Fact]
    public void BlockTargetValidator_AcceptsAbsoluteExe()
    {
        var ok = BlockTargetValidator.TryValidate(
            @"C:\Program Files\App\app.exe",
            BlockItemType.Application,
            out var normalized,
            out var error);

        Assert.True(ok);
        Assert.Equal(@"C:\Program Files\App\app.exe", normalized);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void BlockTargetValidator_RejectsRelativePath()
    {
        var ok = BlockTargetValidator.TryValidate(
            @"tools\app.exe",
            BlockItemType.Application,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("absolute", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlockTargetValidator_RejectsCommandArguments()
    {
        var ok = BlockTargetValidator.TryValidate(
            @"C:\Windows\System32\cmd.exe /c dir",
            BlockItemType.Application,
            out _,
            out var error);

        Assert.False(ok);
        Assert.Contains("arguments", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlockTargetValidator_InferType_ShortcutAndFolder()
    {
        Assert.Equal(
            BlockItemType.Shortcut,
            BlockTargetValidator.InferType(@"C:\Users\me\Desktop\Rider.lnk", isDirectory: false));
        Assert.Equal(
            BlockItemType.Folder,
            BlockTargetValidator.InferType(@"C:\Repos\secret-base", isDirectory: true));
        Assert.Equal(
            BlockItemType.Application,
            BlockTargetValidator.InferType(@"C:\Apps\demo.exe", isDirectory: false));
        Assert.Equal(
            BlockItemType.File,
            BlockTargetValidator.InferType(@"C:\Notes\todo.txt", isDirectory: false));
    }

    [Fact]
    public void BlockTargetValidator_InferDisplayName_StripsLnkExtension()
    {
        Assert.Equal("Rider", BlockTargetValidator.InferDisplayName(@"C:\Desktop\Rider.lnk"));
    }

    [Fact]
    public void DesktopLayout_CreateDefault_HasSchemaV2AndEmptyBlocks()
    {
        var layout = DesktopLayout.CreateDefault();
        Assert.Equal(DesktopLayout.CurrentSchemaVersion, layout.SchemaVersion);
        Assert.Equal(2, layout.SchemaVersion);
        Assert.Equal(2, layout.Widgets.Count);
        Assert.Empty(layout.Blocks);
        Assert.Equal(WidgetTypes.Clock, layout.Widgets[0].Type);
        Assert.Equal(WidgetTypes.Text, layout.Widgets[1].Type);
    }

    [Fact]
    public void DesktopLayout_SupportsMultipleBlocksAndItems()
    {
        var layout = DesktopLayout.CreateDefault();
        var a = DefaultBlockFactory.Create("DEV");
        a.Items.Add(new BlockItem
        {
            Name = "Terminal",
            Type = BlockItemType.Application,
            Target = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe"
        });
        var b = DefaultBlockFactory.Create("EMPTY", x: 500, y: 100);
        layout.Blocks.Add(a);
        layout.Blocks.Add(b);

        Assert.Equal(2, layout.Blocks.Count);
        Assert.Single(layout.Blocks[0].Items);
        Assert.Empty(layout.Blocks[1].Items);
    }
}
