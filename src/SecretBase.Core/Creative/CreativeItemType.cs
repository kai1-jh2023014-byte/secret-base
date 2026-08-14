namespace SecretBase.Core.Creative;

/// <summary>Registered Creative Workspace entry kinds.</summary>
public enum CreativeItemType
{
    File = 0,
    Folder = 1,

    /// <summary>User-marked project folder (no deep project analysis in MVP).</summary>
    Project = 2
}
