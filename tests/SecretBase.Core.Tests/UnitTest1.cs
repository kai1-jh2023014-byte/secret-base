using SecretBase.Core;
using SecretBase.Core.Desktop;
using SecretBase.Core.Security;

namespace SecretBase.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void AppInfo_HasStableProductIdentity()
    {
        Assert.Equal("Secret Base", AppInfo.Name);
        Assert.Equal("SecretBase", AppInfo.ProductId);
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
    }
}

public class RoomIdTests
{
    [Fact]
    public void DefaultRoomId_IsStableForFutureMigration()
    {
        Assert.Equal("default", RoomId.DefaultRoomId.Value);
    }
}

public class SecurityModelTests
{
    [Fact]
    public void PrivilegeLadder_IsOrderedFromObservationToRestricted()
    {
        Assert.True(ActionPrivilege.Observation < ActionPrivilege.SafeAction);
        Assert.True(ActionPrivilege.SafeAction < ActionPrivilege.UserConfirmationRequired);
        Assert.True(ActionPrivilege.UserConfirmationRequired < ActionPrivilege.RestrictedAction);
    }

    [Fact]
    public void TrustBoundaries_IncludeWebPluginAndAiAsSeparateZones()
    {
        Assert.NotEqual(TrustBoundary.TrustedHost, TrustBoundary.WebContent);
        Assert.NotEqual(TrustBoundary.TrustedHost, TrustBoundary.Plugin);
        Assert.NotEqual(TrustBoundary.TrustedHost, TrustBoundary.AiAgent);
    }
}
