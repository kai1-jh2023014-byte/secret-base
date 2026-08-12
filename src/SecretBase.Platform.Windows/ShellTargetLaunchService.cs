using System.Diagnostics;
using SecretBase.Core.Blocks;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Launches user-selected absolute paths via <see cref="ProcessStartInfo.UseShellExecute"/>.
/// No elevation, no arbitrary command lines, no Explorer/shell mutation.
/// </summary>
public sealed class ShellTargetLaunchService : ITargetLaunchService
{
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

        try
        {
            // Documented Process.Start + UseShellExecute: opens the path with its association
            // (.exe runs, .lnk resolves, folders open in Explorer as a normal user action).
            var psi = new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
                ErrorDialog = false
            };

            var process = Process.Start(psi);
            if (process is null && itemType is BlockItemType.Application)
            {
                // Some shell launches return null even on success; treat missing file as failure.
                if (!File.Exists(target) && !Directory.Exists(target))
                {
                    return new TargetLaunchResult(false, "Target path was not found.");
                }
            }

            return new TargetLaunchResult(true, null);
        }
        catch (Exception ex)
        {
            return new TargetLaunchResult(false, ex.Message);
        }
    }
}
