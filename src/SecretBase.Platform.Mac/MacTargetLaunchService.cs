using System.Diagnostics;
using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// Opens user-chosen absolute paths via <c>/usr/bin/open</c> (documented macOS launcher).
/// Never runs free-form shell, never elevates, never touches Dock/Finder internals.
/// </summary>
public sealed class MacTargetLaunchService : ITargetLaunchService
{
    public const string OpenExecutable = "/usr/bin/open";

    private readonly Func<string, IReadOnlyList<string>, TargetLaunchResult> _start;

    public MacTargetLaunchService(Func<string, IReadOnlyList<string>, TargetLaunchResult>? start = null)
    {
        _start = start ?? DefaultStart;
    }

    public TargetLaunchResult TryLaunch(TargetLaunchRequest request)
    {
        if (!Enum.TryParse<BlockItemType>(request.ItemType, ignoreCase: true, out var itemType))
        {
            itemType = BlockItemType.File;
        }

        if (!BlockTargetValidator.TryValidate(request.Target, itemType, out var target, out var error))
        {
            return new TargetLaunchResult(false, error);
        }

        return _start(OpenExecutable, new[] { target });
    }

    internal static TargetLaunchResult DefaultStart(string fileName, IReadOnlyList<string> arguments)
    {
        if (!OperatingSystem.IsMacOS() && fileName == OpenExecutable)
        {
            return new TargetLaunchResult(false, "macOS open is not available on this OS.");
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                ErrorDialog = false
            };
            foreach (var argument in arguments)
            {
                psi.ArgumentList.Add(argument);
            }

            _ = Process.Start(psi);
            return new TargetLaunchResult(true, null);
        }
        catch (Exception ex)
        {
            return new TargetLaunchResult(false, ex.Message);
        }
    }
}
