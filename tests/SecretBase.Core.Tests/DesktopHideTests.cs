using SecretBase.Core.Blocks;

namespace SecretBase.Core.Tests;

public class DesktopHidePolicyTests
{
    [Fact]
    public void DesktopExe_IsHidden_ProtectedInstallIsNot()
    {
        var desktop = Path.Combine(Path.GetTempPath(), "sb-desktop-" + Guid.NewGuid().ToString("N"));
        var program = Path.Combine(Path.GetTempPath(), "sb-program-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(desktop);
        Directory.CreateDirectory(program);
        try
        {
            var exe = Path.Combine(desktop, "Game.exe");
            var install = Path.Combine(program, "Editor.exe");
            File.WriteAllText(exe, "mz");
            File.WriteAllText(install, "mz");

            Assert.True(DesktopHidePolicy.ShouldHideFromDesktop(
                exe, isDirectory: false, [desktop], [program]));
            Assert.False(DesktopHidePolicy.ShouldHideFromDesktop(
                install, isDirectory: false, [desktop], [program]));
            Assert.False(DesktopHidePolicy.ShouldHideFromDesktop(
                desktop, isDirectory: true, [desktop], [program]));
        }
        finally
        {
            Directory.Delete(desktop, recursive: true);
            Directory.Delete(program, recursive: true);
        }
    }
}

public class DesktopItemRelocatorTests
{
    [Fact]
    public void HideThenRestore_RoundTripsDesktopFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "sb-reloc-" + Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(root, "Desktop");
        var storage = Path.Combine(root, "block-items");
        Directory.CreateDirectory(desktop);
        try
        {
            var source = Path.Combine(desktop, "Notes.lnk");
            File.WriteAllText(source, "shortcut");
            var blockId = Guid.NewGuid();
            var itemId = Guid.NewGuid();

            var hidden = DesktopItemRelocator.TryHide(
                source,
                blockId,
                itemId,
                storage,
                [desktop],
                protectedRoots: []);
            Assert.True(hidden.Succeeded);
            Assert.True(hidden.MovedFromSource);
            Assert.False(File.Exists(source));
            Assert.True(File.Exists(hidden.TargetPath));
            Assert.Equal(source, hidden.DesktopOriginPath);

            Assert.True(DesktopItemRelocator.TryRestore(
                hidden.TargetPath,
                hidden.DesktopOriginPath,
                desktop,
                out var restored,
                out var error));
            Assert.Null(error);
            Assert.True(File.Exists(restored));
            Assert.False(File.Exists(hidden.TargetPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Restore_UsesUniqueNameOnCollision()
    {
        var root = Path.Combine(Path.GetTempPath(), "sb-reloc2-" + Guid.NewGuid().ToString("N"));
        var desktop = Path.Combine(root, "Desktop");
        Directory.CreateDirectory(desktop);
        try
        {
            var origin = Path.Combine(desktop, "App.exe");
            File.WriteAllText(origin, "current");
            var stored = Path.Combine(root, "stored.exe");
            File.WriteAllText(stored, "hidden");

            Assert.True(DesktopItemRelocator.TryRestore(stored, origin, desktop, out var restored, out _));
            Assert.True(File.Exists(origin));
            Assert.True(File.Exists(restored));
            Assert.NotEqual(origin, restored);
            Assert.Equal("hidden", File.ReadAllText(restored));
            Assert.Equal("current", File.ReadAllText(origin));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
