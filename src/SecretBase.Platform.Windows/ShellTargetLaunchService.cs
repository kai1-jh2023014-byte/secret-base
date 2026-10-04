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
            // (.exe runs, folders open in Explorer as a normal user action).
            // .lnk files are resolved first so the shortcut's own "Start in" folder is used.
            // Launching the .lnk path alone often ignores that folder after the file is moved
            // off the Desktop, and the target process exits immediately.
            var psi = TryBuildStartInfo(target, itemType);
            if (itemType == BlockItemType.Shortcut
                && !string.Equals(psi.FileName, target, StringComparison.OrdinalIgnoreCase)
                && !File.Exists(psi.FileName)
                && !Directory.Exists(psi.FileName))
            {
                return new TargetLaunchResult(false, "The shortcut's target was not found.");
            }

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

    private static ProcessStartInfo TryBuildStartInfo(string target, BlockItemType itemType)
    {
        if (itemType == BlockItemType.Shortcut
            && target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            && WindowsShortcutReader.TryRead(target, out var resolved)
            && !string.IsNullOrWhiteSpace(resolved.TargetPath))
        {
            var work = resolved.WorkingDirectory;
            if (string.IsNullOrWhiteSpace(work) || !Directory.Exists(work))
            {
                work = Path.GetDirectoryName(resolved.TargetPath);
            }

            return new ProcessStartInfo
            {
                FileName = resolved.TargetPath,
                Arguments = resolved.Arguments ?? string.Empty,
                WorkingDirectory = work ?? string.Empty,
                UseShellExecute = true,
                ErrorDialog = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            ErrorDialog = false
        };
    }
}
