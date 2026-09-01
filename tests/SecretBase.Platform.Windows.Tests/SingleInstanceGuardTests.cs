using SecretBase.Platform.Windows;

namespace SecretBase.Platform.Windows.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_FirstInstance_Succeeds()
    {
        var mutexName = CreateUniqueMutexName();
        using var guard = new WindowsMutexSingleInstanceGuard(mutexName);

        Assert.True(guard.TryAcquire());
        Assert.True(guard.IsHeld);
    }

    [Fact]
    public void TryAcquire_SecondInstance_FailsWhileFirstHeld()
    {
        var mutexName = CreateUniqueMutexName();
        using var first = new WindowsMutexSingleInstanceGuard(mutexName);
        using var second = new WindowsMutexSingleInstanceGuard(mutexName);

        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
        Assert.True(first.IsHeld);
        Assert.False(second.IsHeld);
    }

    [Fact]
    public void Dispose_ReleasesMutex_AllowsReacquire()
    {
        var mutexName = CreateUniqueMutexName();

        var first = new WindowsMutexSingleInstanceGuard(mutexName);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var second = new WindowsMutexSingleInstanceGuard(mutexName);
        Assert.True(second.TryAcquire());
        Assert.True(second.IsHeld);
    }

    [Fact]
    public void TryAcquire_CalledTwiceOnSameGuard_ReturnsTrueWithoutReleasing()
    {
        var mutexName = CreateUniqueMutexName();
        using var guard = new WindowsMutexSingleInstanceGuard(mutexName);

        Assert.True(guard.TryAcquire());
        Assert.True(guard.TryAcquire());
        Assert.True(guard.IsHeld);
    }

    private static string CreateUniqueMutexName() =>
        $@"Local\SecretBase.Tests.{Guid.NewGuid():N}";
}
