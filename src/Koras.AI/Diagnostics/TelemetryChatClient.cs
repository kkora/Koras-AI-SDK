using System.Diagnostics;

namespace Koras.AI;

/// <summary>
/// The outermost built-in decorator: wraps every operation in an <c>ActivitySource</c> span
/// and records duration/token metrics, following the OpenTelemetry GenAI semantic
/// conventions. Message content is never recorded.
/// </summary>
internal sealed class TelemetryChatClient(IChatClient innerClient, string clientName, TimeProvider timeProvider) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using Activity? activity = StartActivity("chat", request.Model);
        long start = timeProvider.GetTimestamp();

        try
        {
            ChatResponse response = await base.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
            RecordSuccess(activity, "chat", request, start, response.Model, response.Usage, response.FinishReason);
            return response;
        }
        catch (Exception ex)
        {
            RecordFailure(activity, "chat", request, start, ex);
            throw;
        }
    }

    public override async IAsyncEnumerable<ChatStreamUpdate> StreamAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using Activity? activity = StartActivity("chat", request.Model);
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
                catch (Exception ex)
                {
                    RecordFailure(activity, "chat", request, start, ex);
                    throw;
                }

                if (update.Usage is { } updateUsage)
                {
                    usage = updateUsage;
                }

                finishReason ??= update.FinishReason;
                yield return update;
            }

            RecordSuccess(activity, "chat", request, start, model: null, usage, finishReason ?? ChatFinishReason.Unknown);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Activity? StartActivity(string operation, string? model)
    {
        Activity? activity = KorasAiDiagnostics.ActivitySource.StartActivity(
            model is null ? operation : $"{operation} {model}",
            ActivityKind.Client);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("gen_ai.operation.name", operation);
            activity.SetTag("gen_ai.system", InnerClient.ProviderName);
            activity.SetTag("koras.ai.client.name", clientName);
            if (model is not null)
            {
                activity.SetTag("gen_ai.request.model", model);
            }
        }

        return activity;
    }

    private void RecordSuccess(
        Activity? activity,
        string operation,
        ChatRequest request,
        long startTimestamp,
        string? model,
        TokenUsage usage,
        ChatFinishReason finishReason)
    {
        double seconds = timeProvider.GetElapsedTime(startTimestamp).TotalSeconds;
        if (activity is { IsAllDataRequested: true })
        {
            if (model is not null)
            {
                activity.SetTag("gen_ai.response.model", model);
            }

            activity.SetTag("gen_ai.usage.input_tokens", usage.InputTokens);
            activity.SetTag("gen_ai.usage.output_tokens", usage.OutputTokens);
            activity.SetTag("gen_ai.response.finish_reasons", new[] { finishReason.Value });
        }

        KorasAiDiagnostics.RecordOperation(operation, InnerClient.ProviderName, clientName, request.Model, seconds, usage, errorType: null);
    }

    private void RecordFailure(Activity? activity, string operation, ChatRequest request, long startTimestamp, Exception exception)
    {
        double seconds = timeProvider.GetElapsedTime(startTimestamp).TotalSeconds;
        string errorType = exception switch
        {
            AiException ai => KorasAiDiagnostics.ErrorType(ai.Code),
            OperationCanceledException => "canceled",
            _ => exception.GetType().Name,
        };

        activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity?.SetTag("error.type", errorType);
        KorasAiDiagnostics.RecordOperation(operation, InnerClient.ProviderName, clientName, request.Model, seconds, default, errorType);
    }
}

/// <summary>Telemetry decorator for embedding clients.</summary>
internal sealed class TelemetryEmbeddingClient(IEmbeddingClient innerClient, string clientName, TimeProvider timeProvider) : IEmbeddingClient
{
    public string ProviderName => innerClient.ProviderName;

    public async Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(request);
        using Activity? activity = KorasAiDiagnostics.ActivitySource.StartActivity(
            request.Model is null ? "embeddings" : $"embeddings {request.Model}",
            ActivityKind.Client);

        if (activity is { IsAllDataRequested: true })
        {
            activity.SetTag("gen_ai.operation.name", "embeddings");
            activity.SetTag("gen_ai.system", innerClient.ProviderName);
            activity.SetTag("koras.ai.client.name", clientName);
            if (request.Model is not null)
            {
                activity.SetTag("gen_ai.request.model", request.Model);
            }
        }

        long start = timeProvider.GetTimestamp();
        try
        {
            EmbeddingResponse response = await innerClient.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            activity?.SetTag("gen_ai.usage.input_tokens", response.Usage.InputTokens);
            KorasAiDiagnostics.RecordOperation(
                "embeddings",
                innerClient.ProviderName,
                clientName,
                request.Model,
                timeProvider.GetElapsedTime(start).TotalSeconds,
                response.Usage,
                errorType: null);
            return response;
        }
        catch (Exception ex)
        {
            string errorType = ex switch
            {
                AiException ai => KorasAiDiagnostics.ErrorType(ai.Code),
                OperationCanceledException => "canceled",
                _ => ex.GetType().Name,
            };
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("error.type", errorType);
            KorasAiDiagnostics.RecordOperation(
                "embeddings",
                innerClient.ProviderName,
                clientName,
                request.Model,
                timeProvider.GetElapsedTime(start).TotalSeconds,
                default,
                errorType);
            throw;
        }
    }
}
