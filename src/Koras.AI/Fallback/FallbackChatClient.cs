using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Koras.AI;

/// <summary>
/// A chat client that tries an ordered list of candidate clients, failing over when a
/// candidate throws a transient <see cref="AiException"/> (or one matching a custom
/// predicate). Terminal errors — authentication, invalid request — propagate immediately by
/// default. Streaming fails over only until the first update has been emitted. When all
/// candidates fail, the last failure is rethrown carrying an <see cref="AggregateException"/>
/// of every attempt.
/// </summary>
public sealed class FallbackChatClient : IChatClient
{
    private readonly IReadOnlyList<IChatClient> _candidates;
    private readonly Func<AiException, bool> _shouldFailover;
    private readonly ILogger _logger;

    /// <summary>Initializes the fallback client.</summary>
    /// <param name="candidates">The candidate clients in preference order (at least one).</param>
    /// <param name="shouldFailover">Decides whether a failure moves to the next candidate; defaults to <see cref="AiException.IsTransient"/>.</param>
    /// <param name="logger">Logs failovers at Warning; silent when omitted.</param>
    public FallbackChatClient(
        IReadOnlyList<IChatClient> candidates,
        Func<AiException, bool>? shouldFailover = null,
        ILogger<FallbackChatClient>? logger = null)
    {
        Guard.NotNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate client is required.", nameof(candidates));
        }

        _candidates = candidates;
        _shouldFailover = shouldFailover ?? (static ex => ex.IsTransient);
        _logger = logger ?? NullLogger<FallbackChatClient>.Instance;
    }

    /// <inheritdoc />
    public string ProviderName => "fallback";

    /// <inheritdoc />
    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        List<Exception>? failures = null;

        for (var i = 0; i < _candidates.Count; i++)
        {
            IChatClient candidate = _candidates[i];
            try
            {
                return await candidate.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (AiException ex) when (i < _candidates.Count - 1 && _shouldFailover(ex))
            {
                (failures ??= []).Add(ex);
                RecordFailover(candidate, _candidates[i + 1], ex);
            }
            catch (AiException ex) when (failures is { Count: > 0 })
            {
                failures.Add(ex);
                throw Exhausted(ex, failures);
            }
        }

        throw new InvalidOperationException("Unreachable: candidate loop always returns or throws.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        List<Exception>? failures = null;

        for (var i = 0; i < _candidates.Count; i++)
        {
            IChatClient candidate = _candidates[i];
            IAsyncEnumerator<ChatStreamUpdate> enumerator = candidate.StreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);

            ChatStreamUpdate? first = null;
            var moved = false;
            try
            {
                moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                if (moved)
                {
                    first = enumerator.Current;
                }
            }
            catch (AiException ex) when (i < _candidates.Count - 1 && _shouldFailover(ex))
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                (failures ??= []).Add(ex);
                RecordFailover(candidate, _candidates[i + 1], ex);
                continue;
            }
            catch (AiException ex) when (failures is { Count: > 0 })
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                failures.Add(ex);
                throw Exhausted(ex, failures);
            }
            catch
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            try
            {
                if (moved && first is not null)
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

    private void RecordFailover(IChatClient from, IChatClient to, AiException exception)
    {
        Log.Failover(_logger, from.ProviderName, to.ProviderName, exception.Code, exception);
        KorasAiDiagnostics.RecordFallback(from.ProviderName, to.ProviderName, exception.Code);
    }

    private static AiException Exhausted(AiException last, List<Exception> failures)
        => new(
            $"All {failures.Count} fallback candidates failed. Last error: {last.Message}",
            last.Code,
            new AggregateException(failures))
        {
            Provider = last.Provider,
            StatusCode = last.StatusCode,
            RetryAfter = last.RetryAfter,
            IsTransient = last.IsTransient,
        };

    private static class Log
    {
        private static readonly Action<ILogger, string, string, AiErrorCode, Exception?> FailoverMessage =
            LoggerMessage.Define<string, string, AiErrorCode>(
                LogLevel.Warning,
                new EventId(3001, "KorasAiFallback"),
                "Koras.AI fallback: provider {FromProvider} failing over to {ToProvider} after {ErrorCode}");

        public static void Failover(ILogger logger, string from, string to, AiErrorCode code, Exception exception)
            => FailoverMessage(logger, from, to, code, exception);
    }
}
