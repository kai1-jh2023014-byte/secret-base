namespace SecretBase.Core.Security;

/// <summary>
/// Future AI / automation privilege ladder. No execution engine in v0.1.
/// </summary>
public enum ActionPrivilege
{
    Observation = 0,
    SafeAction = 1,
    UserConfirmationRequired = 2,
    RestrictedAction = 3
}
