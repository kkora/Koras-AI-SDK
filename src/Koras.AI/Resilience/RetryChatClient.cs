using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koras.AI;

/// <summary>
/// A decorator that retries transient failures with exponential backoff and full jitter,
/// honoring provider <c>Retry-After</c> hints and applying a per-attempt timeout. See
/// <see cref="RetryOptions"/> for defaults. Streaming requests are retried only until the
/// first update has been emitted.
/// </summary>
public sealed class RetryChatClient : DelegatingChatClient
{
    private readonly RetryOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Initializes the decorator.</summary>
    /// <param name="innerClient">The client to wrap.</param>
    /// <param name="options">Retry behavior; defaults apply when omitted.</param>
    /// <param name="timeProvider">The time source for backoff delays (system time when omitted).</param>
    /// <param name="logger">Logs retry scheduling at Warning; silent when omitted.</param>
    public RetryChatClient(
        IChatClient innerClient,
        RetryOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<RetryChatClient>? logger = null)
        : base(innerClient)
    {
        _options = options ?? new RetryOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<RetryChatClient>.Instance;
    }

    /// <inheritdoc />
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ExecuteAttemptAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (AiException ex) when (ShouldRetry(ex, attempt))
            {
                await DelayForRetryAsync(ex, attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);

        for (var attempt = 1; ; attempt++)
        {
            IAsyncEnumerator<ChatStreamUpdate> enumerator =
                InnerClient.StreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);

            ChatStreamUpdate? first = null;
            try
            {
                // Only the window before the first update is retryable: after that, data has
                // been observed and replaying the stream could duplicate output.
                if (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    first = enumerator.Current;
                }
            }
            catch (AiException ex) when (ShouldRetry(ex, attempt))
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                await DelayForRetryAsync(ex, attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            try
            {
                if (first is not null)
                {
                    yield return first;
                    while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        yield return enumerator.Current;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            yield break;
        }
    }

    private async Task<ChatResponse> ExecuteAttemptAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource attemptCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptCts.CancelAfter(_options.AttemptTimeout);

        try
        {
            return await InnerClient.CompleteAsync(request, attemptCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (attemptCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new AiException(
                $"The attempt exceeded the configured timeout of {_options.AttemptTimeout.TotalSeconds:0.#}s.",
                AiErrorCode.Timeout,
                ex)
            {
                Provider = InnerClient.ProviderName,
            };
        }
    }

    private bool ShouldRetry(AiException exception, int attempt)
        => exception.IsTransient && attempt < _options.MaxAttempts;

    private async Task DelayForRetryAsync(AiException exception, int attempt, CancellationToken cancellationToken)
    {
        TimeSpan delay = ComputeDelay(exception, attempt);
        Log.RetryScheduled(_logger, attempt, _options.MaxAttempts, delay.TotalMilliseconds, exception.Code, exception.Provider);
        KorasAiDiagnostics.RecordRetry(InnerClient.ProviderName, exception.Code);

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan ComputeDelay(AiException exception, int attempt)
    {
        if (_options.HonorRetryAfter && exception.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero)
        {
            return retryAfter <= _options.MaxDelay ? retryAfter : _options.MaxDelay;
        }

        double capMs = Math.Min(
            _options.MaxDelay.TotalMilliseconds,
            _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));

        // Full jitter: uniform in [0, cap].
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * capMs);
    }

    private static class Log
    {
        private static readonly Action<ILogger, int, int, double, AiErrorCode, string?, Exception?> RetryScheduledMessage =
            LoggerMessage.Define<int, int, double, AiErrorCode, string?>(
                LogLevel.Warning,
                new EventId(1001, "KorasAiRetryScheduled"),
                "Koras.AI retry {Attempt}/{MaxAttempts} scheduled in {DelayMs:0}ms after {ErrorCode} from provider {Provider}");

        public static void RetryScheduled(ILogger logger, int attempt, int maxAttempts, double delayMs, AiErrorCode code, string? provider)
            => RetryScheduledMessage(logger, attempt, maxAttempts, delayMs, code, provider, null);
    }
}
