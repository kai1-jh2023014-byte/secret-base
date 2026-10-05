namespace SecretBase.Core.Focus;

public enum FocusPhase
{
    Focus = 0,
    ShortBreak = 1,
    LongBreak = 2
}

/// <summary>
/// Local Pomodoro / focus timer state. In-memory only — shared across widgets
/// for the process lifetime so layout reloads do not wipe an active session.
/// </summary>
public sealed class FocusSession
{
    public const int DefaultFocusMinutes = 25;
    public const int DefaultShortBreakMinutes = 5;
    public const int DefaultLongBreakMinutes = 15;
    public const int DefaultRoundsBeforeLongBreak = 4;

    public bool IsRunning { get; init; }

    public bool IsPaused { get; init; }

    public string Label { get; init; } = "Focus";

    public FocusPhase Phase { get; init; } = FocusPhase.Focus;

    /// <summary>1-based focus round within the current cycle (resets after a long break).</summary>
    public int Round { get; init; } = 1;

    public int CompletedFocusRounds { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(DefaultFocusMinutes);

    /// <summary>Remaining time captured when the session was paused.</summary>
    public TimeSpan RemainingAtPause { get; init; }

    public TimeSpan FocusDuration { get; init; } = TimeSpan.FromMinutes(DefaultFocusMinutes);

    public TimeSpan ShortBreakDuration { get; init; } = TimeSpan.FromMinutes(DefaultShortBreakMinutes);

    public TimeSpan LongBreakDuration { get; init; } = TimeSpan.FromMinutes(DefaultLongBreakMinutes);

    public int RoundsBeforeLongBreak { get; init; } = DefaultRoundsBeforeLongBreak;

    public TimeSpan Remaining(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            return Duration;
        }

        if (IsPaused)
        {
            return RemainingAtPause < TimeSpan.Zero ? TimeSpan.Zero : RemainingAtPause;
        }

        if (StartedAt is null)
        {
            return Duration;
        }

        var elapsed = now - StartedAt.Value;
        var left = Duration - elapsed;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    public bool IsComplete(DateTimeOffset now) => IsRunning && !IsPaused && Remaining(now) == TimeSpan.Zero;

    public string PhaseLabel =>
        Phase switch
        {
            FocusPhase.ShortBreak => "Short break",
            FocusPhase.LongBreak => "Long break",
            _ => "Focus"
        };

    public string StatusLine(DateTimeOffset now)
    {
        if (!IsRunning)
        {
            return "Focus idle";
        }

        var remaining = Remaining(now);
        var mmss = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
        if (IsPaused)
        {
            return $"{Label} paused {mmss}";
        }

        if (remaining == TimeSpan.Zero)
        {
            return $"{Label} · {PhaseLabel} complete";
        }

        return $"{Label} · {PhaseLabel} {mmss}";
    }

    public string RoundProgressLine =>
        Phase is FocusPhase.Focus
            ? $"Round {Round}/{RoundsBeforeLongBreak}"
            : $"After round {Math.Max(1, CompletedFocusRounds)} · {PhaseLabel}";
}

public sealed class FocusStartResult
{
    public required FocusSession Session { get; init; }

    public bool AlreadyRunning { get; init; }

    public bool Resumed { get; init; }
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

    /// <summary>
    /// Updates preferred focus / break lengths. When idle, also updates the displayed duration.
    /// When running, future phases pick up the new break/focus lengths.
    /// </summary>
    public FocusSession ConfigureDurations(
        TimeSpan? focus = null,
        TimeSpan? shortBreak = null,
        TimeSpan? longBreak = null)
    {
        lock (_gate)
        {
            var focusDuration = ClampMinutes(focus ?? _current.FocusDuration, 1, 120);
            var shortBreakDuration = ClampMinutes(
                shortBreak ?? _current.ShortBreakDuration,
                1,
                60);
            var longBreakDuration = ClampMinutes(
                longBreak ?? _current.LongBreakDuration,
                1,
                60);

            if (_current.IsRunning)
            {
                _current = new FocusSession
                {
                    IsRunning = _current.IsRunning,
                    IsPaused = _current.IsPaused,
                    Label = _current.Label,
                    Phase = _current.Phase,
                    Round = _current.Round,
                    CompletedFocusRounds = _current.CompletedFocusRounds,
                    StartedAt = _current.StartedAt,
                    Duration = _current.Duration,
                    RemainingAtPause = _current.RemainingAtPause,
                    FocusDuration = focusDuration,
                    ShortBreakDuration = shortBreakDuration,
                    LongBreakDuration = longBreakDuration,
                    RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
                };
                return _current;
            }

            _current = new FocusSession
            {
                Duration = focusDuration,
                FocusDuration = focusDuration,
                ShortBreakDuration = shortBreakDuration,
                LongBreakDuration = longBreakDuration,
                RoundsBeforeLongBreak = FocusSession.DefaultRoundsBeforeLongBreak
            };
            return _current;
        }
    }

    /// <summary>
    /// Starts a Pomodoro focus phase. If a session is already running, returns it
    /// without stacking. If paused, resumes instead of restarting.
    /// </summary>
    public FocusStartResult Start(
        DateTimeOffset now,
        TimeSpan? duration = null,
        string? label = null,
        TimeSpan? shortBreak = null,
        TimeSpan? longBreak = null)
    {
        lock (_gate)
        {
            if (_current.IsRunning && _current.IsPaused)
            {
                _current = ResumeUnlocked(now);
                return new FocusStartResult { Session = _current, Resumed = true };
            }

            if (_current.IsRunning)
            {
                return new FocusStartResult { Session = _current, AlreadyRunning = true };
            }

            var focusDuration = ClampMinutes(
                duration ?? _current.FocusDuration,
                1,
                120);
            var shortBreakDuration = ClampMinutes(
                shortBreak ?? _current.ShortBreakDuration,
                1,
                60);
            var longBreakDuration = ClampMinutes(
                longBreak ?? _current.LongBreakDuration,
                1,
                60);

            _current = new FocusSession
            {
                IsRunning = true,
                Label = string.IsNullOrWhiteSpace(label) ? "Pomodoro" : label.Trim(),
                Phase = FocusPhase.Focus,
                Round = 1,
                StartedAt = now,
                Duration = focusDuration,
                FocusDuration = focusDuration,
                ShortBreakDuration = shortBreakDuration,
                LongBreakDuration = longBreakDuration,
                RoundsBeforeLongBreak = FocusSession.DefaultRoundsBeforeLongBreak
            };
            return new FocusStartResult { Session = _current };
        }
    }

    private static TimeSpan ClampMinutes(TimeSpan value, int minMinutes, int maxMinutes)
    {
        var minutes = (int)Math.Round(value.TotalMinutes);
        minutes = Math.Clamp(minutes, minMinutes, maxMinutes);
        return TimeSpan.FromMinutes(minutes);
    }

    public FocusSession Pause(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_current.IsRunning || _current.IsPaused)
            {
                return _current;
            }

            _current = new FocusSession
            {
                IsRunning = true,
                IsPaused = true,
                Label = _current.Label,
                Phase = _current.Phase,
                Round = _current.Round,
                CompletedFocusRounds = _current.CompletedFocusRounds,
                StartedAt = _current.StartedAt,
                Duration = _current.Duration,
                RemainingAtPause = _current.Remaining(now),
                FocusDuration = _current.FocusDuration,
                ShortBreakDuration = _current.ShortBreakDuration,
                LongBreakDuration = _current.LongBreakDuration,
                RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
            };
            return _current;
        }
    }

    public FocusSession Resume(DateTimeOffset now)
    {
        lock (_gate)
        {
            return ResumeUnlocked(now);
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

    public FocusSession Reset(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_current.IsRunning)
            {
                return _current;
            }

            var duration = PhaseDuration(_current.Phase, _current);
            _current = new FocusSession
            {
                IsRunning = true,
                Label = _current.Label,
                Phase = _current.Phase,
                Round = _current.Round,
                CompletedFocusRounds = _current.CompletedFocusRounds,
                StartedAt = now,
                Duration = duration,
                FocusDuration = _current.FocusDuration,
                ShortBreakDuration = _current.ShortBreakDuration,
                LongBreakDuration = _current.LongBreakDuration,
                RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
            };
            return _current;
        }
    }

    /// <summary>
    /// When the current phase timer hits zero, advances to the next phase and starts it.
    /// No-op if the phase is still running.
    /// </summary>
    public FocusSession AdvanceIfComplete(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_current.IsRunning || _current.IsPaused || !_current.IsComplete(now))
            {
                return _current;
            }

            if (_current.Phase == FocusPhase.Focus)
            {
                var completed = _current.CompletedFocusRounds + 1;
                var longBreak = completed % _current.RoundsBeforeLongBreak == 0;
                var nextPhase = longBreak ? FocusPhase.LongBreak : FocusPhase.ShortBreak;
                var duration = PhaseDuration(nextPhase, _current);
                _current = new FocusSession
                {
                    IsRunning = true,
                    Label = _current.Label,
                    Phase = nextPhase,
                    Round = _current.Round,
                    CompletedFocusRounds = completed,
                    StartedAt = now,
                    Duration = duration,
                    FocusDuration = _current.FocusDuration,
                    ShortBreakDuration = _current.ShortBreakDuration,
                    LongBreakDuration = _current.LongBreakDuration,
                    RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
                };
                return _current;
            }

            var nextRound = _current.Phase == FocusPhase.LongBreak
                ? 1
                : Math.Min(_current.RoundsBeforeLongBreak, _current.Round + 1);
            var focusDuration = _current.FocusDuration;
            _current = new FocusSession
            {
                IsRunning = true,
                Label = _current.Label,
                Phase = FocusPhase.Focus,
                Round = nextRound,
                CompletedFocusRounds = _current.Phase == FocusPhase.LongBreak
                    ? _current.CompletedFocusRounds
                    : _current.CompletedFocusRounds,
                StartedAt = now,
                Duration = focusDuration,
                FocusDuration = focusDuration,
                ShortBreakDuration = _current.ShortBreakDuration,
                LongBreakDuration = _current.LongBreakDuration,
                RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
            };
            return _current;
        }
    }

    private FocusSession ResumeUnlocked(DateTimeOffset now)
    {
        if (!_current.IsRunning || !_current.IsPaused)
        {
            return _current;
        }

        var remaining = _current.RemainingAtPause;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        _current = new FocusSession
        {
            IsRunning = true,
            Label = _current.Label,
            Phase = _current.Phase,
            Round = _current.Round,
            CompletedFocusRounds = _current.CompletedFocusRounds,
            StartedAt = now,
            Duration = remaining,
            RemainingAtPause = TimeSpan.Zero,
            FocusDuration = _current.FocusDuration,
            ShortBreakDuration = _current.ShortBreakDuration,
            LongBreakDuration = _current.LongBreakDuration,
            RoundsBeforeLongBreak = _current.RoundsBeforeLongBreak
        };
        return _current;
    }

    private static TimeSpan PhaseDuration(FocusPhase phase, FocusSession template) =>
        phase switch
        {
            FocusPhase.ShortBreak => template.ShortBreakDuration,
            FocusPhase.LongBreak => template.LongBreakDuration,
            _ => template.FocusDuration
        };
}
