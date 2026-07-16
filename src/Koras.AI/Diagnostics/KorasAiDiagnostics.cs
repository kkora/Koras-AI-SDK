using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Koras.AI;

/// <summary>The SDK's shared telemetry instruments (see docs/architecture/observability.md).</summary>
internal static class KorasAiDiagnostics
{
    public static readonly ActivitySource ActivitySource = new(KorasAiTelemetry.ActivitySourceName);

    private static readonly Meter Meter = new(KorasAiTelemetry.MeterName);

    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "koras.ai.client.operation.duration",
        unit: "s",
        description: "Duration of Koras.AI client operations.");

    private static readonly Counter<long> TokenUsageCounter = Meter.CreateCounter<long>(
        "koras.ai.client.token.usage",
        unit: "{token}",
        description: "Input and output tokens consumed by Koras.AI client operations.");

    private static readonly Counter<long> Retries = Meter.CreateCounter<long>(
        "koras.ai.client.retries",
        unit: "{retry}",
        description: "Retries performed by the Koras.AI retry decorator.");

    private static readonly Counter<long> Fallbacks = Meter.CreateCounter<long>(
        "koras.ai.client.fallbacks",
        unit: "{fallback}",
        description: "Failovers performed by the Koras.AI fallback client.");

    public static void RecordOperation(
        string operation,
        string provider,
        string? clientName,
        string? model,
        double durationSeconds,
        TokenUsage usage,
        string? errorType)
    {
        var tags = new TagList
        {
            { "gen_ai.operation.name", operation },
            { "gen_ai.system", provider },
            { "koras.ai.client.name", clientName },
            { "gen_ai.request.model", model },
        };
        if (errorType is not null)
        {
            tags.Add("error.type", errorType);
        }

        OperationDuration.Record(durationSeconds, tags);

        if (usage.InputTokens > 0)
        {
            var inputTags = tags;
            inputTags.Add("gen_ai.token.type", "input");
            TokenUsageCounter.Add(usage.InputTokens, inputTags);
        }

        if (usage.OutputTokens > 0)
        {
            var outputTags = tags;
            outputTags.Add("gen_ai.token.type", "output");
            TokenUsageCounter.Add(usage.OutputTokens, outputTags);
        }
    }

    public static void RecordRetry(string provider, AiErrorCode code)
        => Retries.Add(1, new TagList
        {
            { "gen_ai.system", provider },
            { "error.type", ErrorType(code) },
        });

    public static void RecordFallback(string fromProvider, string toProvider, AiErrorCode code)
        => Fallbacks.Add(1, new TagList
        {
            { "koras.ai.fallback.from", fromProvider },
            { "koras.ai.fallback.to", toProvider },
            { "error.type", ErrorType(code) },
        });

    public static string ErrorType(AiErrorCode code) => code switch
    {
        AiErrorCode.Authentication => "authentication",
        AiErrorCode.PermissionDenied => "permission_denied",
        AiErrorCode.ModelNotFound => "model_not_found",
        AiErrorCode.InvalidRequest => "invalid_request",
        AiErrorCode.ContentFiltered => "content_filtered",
        AiErrorCode.RateLimited => "rate_limited",
        AiErrorCode.ProviderUnavailable => "provider_unavailable",
        AiErrorCode.Network => "network",
        AiErrorCode.Timeout => "timeout",
        AiErrorCode.Canceled => "canceled",
        AiErrorCode.InvalidResponse => "invalid_response",
        AiErrorCode.NotSupported => "not_supported",
        AiErrorCode.ToolExecutionFailed => "tool_execution_failed",
        AiErrorCode.Configuration => "configuration",
        _ => "unknown",
    };
}
