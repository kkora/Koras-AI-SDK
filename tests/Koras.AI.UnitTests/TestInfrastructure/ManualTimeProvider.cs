namespace Koras.AI.UnitTests.TestInfrastructure;

/// <summary>
/// A deterministic TimeProvider for testing time-dependent behavior: time advances only via
/// <see cref="Advance"/>, which fires any timers that come due.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _lock = new();
    private readonly List<ManualTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public int ActiveTimerCount
    {
        get
        {
            lock (_lock)
            {
                return _timers.Count(static t => t.DueAt is not null);
            }
        }
    }

    public List<TimeSpan> RequestedDelays { get; } = [];

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
        {
            return _now;
        }
    }

    public override long GetTimestamp() => GetUtcNow().UtcTicks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        lock (_lock)
        {
            _timers.Add(timer);
            if (dueTime > TimeSpan.Zero)
            {
                RequestedDelays.Add(dueTime);
            }
        }

        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        List<ManualTimer> due;
        lock (_lock)
        {
            _now += delta;
            due = _timers.Where(t => t.DueAt is { } dueAt && dueAt <= _now).ToList();
            foreach (ManualTimer timer in due)
            {
                timer.MarkFired();
            }
        }

        foreach (ManualTimer timer in due)
        {
            timer.Fire();
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
            if (dueTime == TimeSpan.Zero)
            {
                MarkFired();
                Fire();
            }

            return true;
        }

        public void MarkFired() => DueAt = null;

        public void Fire() => callback(state);

        public void Dispose() => DueAt = null;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
