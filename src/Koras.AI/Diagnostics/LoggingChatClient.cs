using Microsoft.Extensions.Logging;

namespace Koras.AI;

/// <summary>
/// Logs operation start/completion/failure with provider, model, duration, and token counts.
/// Message content is logged only at Trace level and only when
/// <see cref="KorasAiTelemetryOptions.EnableSensitiveData"/> is enabled.
/// </summary>
internal sealed partial class LoggingChatClient(
    IChatClient innerClient,
    ILogger logger,
    KorasAiTelemetryOptions telemetryOptions,
    TimeProvider timeProvider) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        LogStart(request, streaming: false);
        long start = timeProvider.GetTimestamp();

        try
        {
            ChatResponse response = await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            LogCompleted(
                logger,
                ProviderName,
                response.Model ?? request.Model,
                timeProvider.GetElapsedTime(start).TotalMilliseconds,
                response.Usage.InputTokens,
                response.Usage.OutputTokens,
                response.FinishReason.Value);

            if (telemetryOptions.EnableSensitiveData && logger.IsEnabled(LogLevel.Trace))
            {
                LogResponseContent(logger, response.Text);
            }

            return response;
        }
        catch (AiException ex)
        {
            LogFailed(logger, ProviderName, request.Model, ex.Code, ex.StatusCode, ex.RequestId, ex);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        LogStart(request, streaming: true);
        long start = timeProvider.GetTimestamp();

        TokenUsage usage = default;
        ChatFinishReason? finishReason = null;
        IAsyncEnumerator<ChatStreamUpdate> enumerator = base.StreamAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ChatStreamUpdate update;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (AiException ex)
                {
                    LogFailed(logger, ProviderName, request.Model, ex.Code, ex.StatusCode, ex.RequestId, ex);
                    throw;
                }

                if (update.Usage is { } updateUsage)
                {
                    usage = updateUsage;
                }

                finishReason ??= update.FinishReason;
                yield return update;
            }

            LogCompleted(
                logger,
                ProviderName,
                request.Model,
                timeProvider.GetElapsedTime(start).TotalMilliseconds,
                usage.InputTokens,
                usage.OutputTokens,
                (finishReason ?? ChatFinishReason.Unknown).Value);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void LogStart(ChatRequest request, bool streaming)
    {
        LogStarting(logger, ProviderName, request.Model, streaming, request.Messages.Count);
        if (telemetryOptions.EnableSensitiveData && logger.IsEnabled(LogLevel.Trace))
        {
            foreach (ChatMessage message in request.Messages)
            {
                LogRequestContent(logger, message.Role.Value, message.Text);
            }
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Koras.AI chat starting: provider={Provider} model={Model} streaming={Streaming} messages={MessageCount}")]
    private static partial void LogStarting(ILogger logger, string provider, string? model, bool streaming, int messageCount);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information,
        Message = "Koras.AI chat completed: provider={Provider} model={Model} duration={DurationMs:0}ms inputTokens={InputTokens} outputTokens={OutputTokens} finishReason={FinishReason}")]
    private static partial void LogCompleted(ILogger logger, string provider, string? model, double durationMs, int inputTokens, int outputTokens, string finishReason);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Koras.AI chat failed: provider={Provider} model={Model} code={Code} status={StatusCode} requestId={RequestId}")]
    private static partial void LogFailed(ILogger logger, string provider, string? model, AiErrorCode code, int? statusCode, string? requestId, Exception exception);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Trace,
        Message = "Koras.AI request content (EnableSensitiveData=true): role={Role} content={Content}")]
    private static partial void LogRequestContent(ILogger logger, string role, string? content);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Trace,
        Message = "Koras.AI response content (EnableSensitiveData=true): {Content}")]
    private static partial void LogResponseContent(ILogger logger, string? content);
}
