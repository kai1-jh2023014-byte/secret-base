namespace SecretBase.Core.Blocks;

/// <summary>
/// Builds and appends a <see cref="BlockItem"/> after a successful intake/link.
/// UI/Platform supply the intake result; Core stays free of OS APIs.
/// </summary>
public static class BlockItemAdder
{
    public readonly record struct IntakeSnapshot(
        bool Succeeded,
        string? TargetPath,
        bool MovedFromSource,
        string? ErrorMessage,
        string? DesktopOriginPath);

    /// <summary>
    /// Validates source + intake snapshot and appends a new item when the target is new.
    /// </summary>
    public static bool TryAppend(
        Block block,
        string sourceAbsolutePath,
        bool sourceIsDirectory,
        Guid itemId,
        IntakeSnapshot intake,
        out BlockItem? item,
        out string error,
        out bool duplicate)
    {
        item = null;
        error = string.Empty;
        duplicate = false;

        if (block is null)
        {
            error = "Block is missing.";
            return false;
        }

        var type = BlockTargetValidator.InferType(sourceAbsolutePath, sourceIsDirectory);
        if (!BlockTargetValidator.TryValidate(sourceAbsolutePath, type, out var normalized, out error))
        {
            return false;
        }

        if (!intake.Succeeded || string.IsNullOrWhiteSpace(intake.TargetPath))
        {
            error = intake.ErrorMessage ?? "Could not add item.";
            return false;
        }

        if (!BlockTargetValidator.TryValidate(intake.TargetPath, type, out var target, out error))
        {
            return false;
        }

        if (block.Items.Any(i => string.Equals(i.Target, target, StringComparison.OrdinalIgnoreCase)))
        {
            duplicate = true;
            error = "That app is already in this Block.";
            return false;
        }

        type = BlockTargetValidator.InferType(target, Directory.Exists(target));
        var displaySource = intake.MovedFromSource ? normalized : target;
        item = new BlockItem
        {
            Id = itemId,
            Name = BlockTargetValidator.InferDisplayName(displaySource),
            Type = type,
            Target = target,
            Icon = string.Empty,
            DesktopOriginPath = intake.DesktopOriginPath,
            HiddenFromDesktop = intake.MovedFromSource,
            X = -1,
            Y = -1
        };

        block.Items.Add(item);
        return true;
    }
}
