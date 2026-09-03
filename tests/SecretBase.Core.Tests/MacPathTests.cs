using SecretBase.Core;
using SecretBase.Core.Ai;
using SecretBase.Core.Blocks;
using SecretBase.Core.Creative;

namespace SecretBase.Core.Tests;

public class HostPathTests
{
    [Fact]
    public void AcceptsWindowsAndPosixAbsolutePaths()
    {
        Assert.True(HostPath.IsWindowsDriveAbsolute(@"C:\Apps\demo.exe"));
        Assert.True(HostPath.IsPosixAbsolute("/Applications/Cursor.app"));
        Assert.True(HostPath.IsAbsolute("/Users/demo/Documents"));
        Assert.False(HostPath.IsAbsolute("relative/path"));
    }

    [Fact]
    public void NormalizeSeparators_PreservesPathKind()
    {
        Assert.Equal(@"C:\Users\demo\file.txt", HostPath.NormalizeSeparators(@"C:/Users/demo/file.txt"));
        Assert.Equal("/Users/demo/file.txt", HostPath.NormalizeSeparators("/Users/demo/file.txt"));
    }

    [Fact]
    public void GetFileName_WorksForPosixAppBundle()
    {
        Assert.Equal("Cursor.app", HostPath.GetFileName("/Applications/Cursor.app"));
        Assert.True(HostPath.IsMacAppBundle("/Applications/Cursor.app/"));
    }
}

public class BlockTargetValidatorMacTests
{
    [Fact]
    public void AcceptsPosixAbsoluteAppBundle()
    {
        var ok = BlockTargetValidator.TryValidate(
            "/Applications/Cursor.app",
            BlockItemType.Application,
            out var normalized,
            out var error);

        Assert.True(ok, error);
        Assert.Equal("/Applications/Cursor.app", normalized);
    }

    [Fact]
    public void InferType_TreatsAppBundleAsApplication()
    {
        Assert.Equal(
            BlockItemType.Application,
            BlockTargetValidator.InferType("/Applications/Safari.app", isDirectory: true));
        Assert.Equal(
            BlockItemType.Shortcut,
            BlockTargetValidator.InferType("/Users/me/Desktop/Docs.webloc", isDirectory: false));
        Assert.Equal("Safari", BlockTargetValidator.InferDisplayName("/Applications/Safari.app"));
    }

    [Fact]
    public void RejectsRelativeUnixPath()
    {
        var ok = BlockTargetValidator.TryValidate(
            "Applications/Cursor.app",
            BlockItemType.Application,
            out _,
            out var error);
        Assert.False(ok);
        Assert.Contains("absolute", error, StringComparison.OrdinalIgnoreCase);
    }
}

public class CreativePathValidatorMacTests
{
    [Fact]
    public void TryNormalize_AcceptsPosixFolder()
    {
        Assert.True(CreativePathValidator.TryNormalize(
            "/Users/demo/Projects/",
            CreativeItemType.Folder,
            out var folder,
            out _));
        Assert.Equal("/Users/demo/Projects", folder);
    }
}

public class CursorInstallLocatorMacTests
{
    [Fact]
    public void ResolvesMacApplicationsBundle_WithoutHardcodedUser()
    {
        var apps = "/Applications";
        var expected = Path.Combine(apps, "Cursor.app", "Contents", "MacOS", "Cursor");
        var hit = CursorInstallLocator.TryResolve(
            pathEnvironment: null,
            localAppData: null,
            fileExists: p => string.Equals(p, expected, StringComparison.Ordinal),
            homeDirectory: "/Users/demo",
            applicationsDirectory: apps);

        Assert.Equal(expected, hit);
    }

    [Fact]
    public void ResolvesHomeApplicationsBundle()
    {
        var home = "/Users/demo";
        var expected = Path.Combine(home, "Applications", "Cursor.app");
        var hit = CursorInstallLocator.TryResolve(
            pathEnvironment: null,
            localAppData: null,
            fileExists: p => string.Equals(p, expected, StringComparison.Ordinal),
            homeDirectory: home,
            applicationsDirectory: "/empty-apps");

        Assert.Equal(expected, hit);
    }
}
