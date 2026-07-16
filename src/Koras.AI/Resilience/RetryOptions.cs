namespace Koras.AI;

/// <summary>
/// Configures the retry decorator added by <see cref="KorasAiBuilder.UseRetry"/>. Only
/// failures with <see cref="AiException.IsTransient"/> are retried; streaming operations are
/// retried only if they fail before the first update is emitted. Backoff is exponential with
/// full jitter, and provider <c>Retry-After</c> hints are honored when
/// <see cref="HonorRetryAfter"/> is enabled.
/// </summary>
public sealed class RetryOptions
{
    private int _maxAttempts = 3;
    private TimeSpan _baseDelay = TimeSpan.FromSeconds(1);
    private TimeSpan _maxDelay = TimeSpan.FromSeconds(30);
    private TimeSpan _attemptTimeout = TimeSpan.FromSeconds(100);

    /// <summary>Total attempts including the first (default 3). Must be at least 1.</summary>
    public int MaxAttempts
    {
        get => _maxAttempts;
        set => _maxAttempts = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), "MaxAttempts must be at least 1.");
    }

    /// <summary>The backoff base delay (default 1 second). Attempt n waits up to BaseDelay × 2ⁿ⁻¹, jittered.</summary>
    public TimeSpan BaseDelay
    {
        get => _baseDelay;
        set => _baseDelay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), "BaseDelay must not be negative.");
    }

    /// <summary>The upper bound for any computed backoff delay (default 30 seconds).</summary>
    public TimeSpan MaxDelay
    {
        get => _maxDelay;
        set => _maxDelay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), "MaxDelay must not be negative.");
    }

    /// <summary>The timeout applied to each individual attempt (default 100 seconds). Timeouts count as transient failures.</summary>
    public TimeSpan AttemptTimeout
    {
        get => _attemptTimeout;
        set => _attemptTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), "AttemptTimeout must be positive.");
    }

    /// <summary>Whether a provider-supplied <see cref="AiException.RetryAfter"/> overrides the computed backoff (default true).</summary>
    public bool HonorRetryAfter { get; set; } = true;
}
