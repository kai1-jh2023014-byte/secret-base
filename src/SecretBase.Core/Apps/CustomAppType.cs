namespace SecretBase.Core.Apps;

/// <summary>How a registered custom app is launched. Not a plugin / admin grant.</summary>
public enum CustomAppType
{
    Application = 0,
    Folder = 1,
    Website = 2
}
