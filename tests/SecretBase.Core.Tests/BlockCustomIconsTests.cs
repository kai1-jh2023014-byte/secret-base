using SecretBase.Core.Blocks;

namespace SecretBase.Core.Tests;

public class BlockCustomIconsTests
{
    [Theory]
    [InlineData("photo.png", true)]
    [InlineData("photo.JPG", true)]
    [InlineData("icon.ico", true)]
    [InlineData("shot.webp", true)]
    [InlineData("notes.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAllowedImagePath_FiltersByExtension(string? path, bool expected)
    {
        Assert.Equal(expected, BlockCustomIcons.IsAllowedImagePath(path));
    }

    [Fact]
    public void IsCustomIconPath_RequiresPathUnderCustomRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "secretbase-icons-test", "custom");
        Directory.CreateDirectory(root);
        var inside = Path.Combine(root, "item.png");
        var outside = Path.Combine(Path.GetTempPath(), "other.png");

        Assert.True(BlockCustomIcons.IsCustomIconPath(inside, root));
        Assert.False(BlockCustomIcons.IsCustomIconPath(outside, root));
        Assert.False(BlockCustomIcons.IsCustomIconPath(inside, null));
    }

    [Fact]
    public void GlyphFor_UsesNameInitialOrTypeFallback()
    {
        Assert.Equal("C", BlockCustomIcons.GlyphFor(BlockItemType.File, "chrome"));
        Assert.Equal("D", BlockCustomIcons.GlyphFor(BlockItemType.Folder, "!!!"));
        Assert.Equal("L", BlockCustomIcons.GlyphFor(BlockItemType.Shortcut, null));
    }

    [Fact]
    public void Presets_ExposeStableIds()
    {
        Assert.Contains(BlockCustomIcons.Presets, p => p.Id == "ocean");
        Assert.All(BlockCustomIcons.Presets, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Id));
            Assert.False(string.IsNullOrWhiteSpace(p.DisplayName));
            Assert.StartsWith("#", p.HexColor);
        });
    }

    [Fact]
    public void BlockIconPng_WritesReadablePngFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "secretbase-png-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "tile.png");
        try
        {
            Assert.True(BlockIconPng.TryWriteSolidTile(path, 64, 255, 47, 111, 237, out var error), error);
            Assert.True(File.Exists(path));
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 32);
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'N', bytes[2]);
            Assert.Equal((byte)'G', bytes[3]);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
