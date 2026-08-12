namespace SecretBase.Core.Blocks;

/// <summary>
/// Kind of launchable target inside a Block. V1 prioritizes Application and Shortcut.
/// </summary>
public enum BlockItemType
{
    Application = 0,
    Shortcut = 1,
    File = 2,
    Folder = 3
}
