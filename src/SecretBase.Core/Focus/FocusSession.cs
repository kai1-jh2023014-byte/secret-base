namespace SecretBase.Core.Focus;

public sealed class FocusSession
{
    public bool IsRunning { get; init; }

    public string Label { get; init; } = "Focus";

    public DateTimeOffset? StartedAt { get; init; }

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(25);

    public TimeSpan Remaining(DateTimeOffset now)
    {
        if (!IsRunning || StartedAt is null)
        {
            return Duration;
        }

        var elapsed = now - StartedAt.Value;
        var left = Duration - elapsed;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    public bool IsComplete(DateTimeOffset now) => IsRunning && Remaining(now) == TimeSpan.Zero;

    public string StatusLine(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            return "Focus idle";
        }

        var remaining = Remaining(now);
        if (remaining == TimeSpan.Zero)
        {
            return $"{Label} complete";
        }

        return $"{Label} {remaining.Minutes:00}:{remaining.Seconds:00}";
    }
}

public sealed class FocusSessionStore
{
    private readonly object _gate = new();
    private FocusSession _current = new();

    public FocusSession Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public FocusSession Start(DateTimeOffset now, TimeSpan? duration = null, string? label = null)
    {
        lock (_gate)
        {
            _current = new FocusSession
            {
                IsRunning = true,
                Label = string.IsNullOrWhiteSpace(label) ? "Pomodoro" : label.Trim(),
                StartedAt = now,
                Duration = duration ?? TimeSpan.FromMinutes(25)
            };
            return _current;
        }
    }

    public FocusSession Stop()
    {
        lock (_gate)
        {
            _current = new FocusSession();
            return _current;
        }
    }
}
