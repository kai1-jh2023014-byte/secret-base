namespace SecretBase.Core.Time;

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTimeOffset GetLocalNow() => DateTimeOffset.Now;
}
