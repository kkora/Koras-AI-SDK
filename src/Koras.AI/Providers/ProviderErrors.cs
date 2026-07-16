using System.Globalization;
using System.Net.Http.Headers;

namespace Koras.AI.Providers;

/// <summary>
/// Factory helpers that normalize provider failures into <see cref="AiException"/>. Used by
/// the built-in providers and available to custom provider authors so all providers share the
/// same error semantics.
/// </summary>
public static class ProviderErrors
{
    private const int MaxBodyLength = 4096;

    /// <summary>Builds a normalized exception from an HTTP error response.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="body">The response body (truncated and attached as diagnostics).</param>
    /// <param name="retryAfter">The parsed retry hint, when present.</param>
    /// <param name="requestId">The provider request id, when present.</param>
    /// <param name="message">A provider-extracted error message; a generic one is composed when omitted.</param>
    public static AiException FromHttpResponse(
        string provider,
        int statusCode,
        string? body,
        TimeSpan? retryAfter = null,
        string? requestId = null,
        string? message = null)
    {
        Guard.NotNullOrWhiteSpace(provider);
        AiErrorCode code = MapStatusCode(statusCode);
        message ??= $"The {provider} request failed with HTTP {statusCode}.";

        return new AiException(message, code)
        {
            Provider = provider,
            StatusCode = statusCode,
            RetryAfter = retryAfter,
            RequestId = requestId,
            ProviderErrorBody = Truncate(body),
        };
    }

    /// <summary>Builds a normalized exception for a network-level failure.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="innerException">The underlying transport exception.</param>
    /// <param name="hint">An optional remediation hint appended to the message.</param>
    public static AiException Network(string provider, Exception innerException, string? hint = null)
    {
        Guard.NotNullOrWhiteSpace(provider);
        var message = $"The {provider} request failed at the network layer: {innerException.Message}";
        if (hint is not null)
        {
            message += $" {hint}";
        }

        return new AiException(message, AiErrorCode.Network, innerException) { Provider = provider };
    }

    /// <summary>Builds a normalized exception for an unparseable success payload.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="body">The payload that failed to parse (truncated and attached).</param>
    /// <param name="innerException">The parse exception.</param>
    public static AiException InvalidResponse(string provider, string? body, Exception? innerException = null)
        => new($"The {provider} response could not be parsed.", AiErrorCode.InvalidResponse, innerException)
        {
            Provider = provider,
            ProviderErrorBody = Truncate(body),
        };

    /// <summary>Builds a normalized exception for a capability the provider does not offer.</summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="capability">The unsupported capability (for example <c>"embeddings"</c>).</param>
    public static AiException NotSupported(string provider, string capability)
        => new($"The {provider} provider does not support {capability}.", AiErrorCode.NotSupported)
        {
            Provider = provider,
        };

    /// <summary>Maps an HTTP status code to the provider-neutral taxonomy.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    public static AiErrorCode MapStatusCode(int statusCode) => statusCode switch
    {
        401 => AiErrorCode.Authentication,
        403 => AiErrorCode.PermissionDenied,
        404 => AiErrorCode.ModelNotFound,
        408 => AiErrorCode.Timeout,
        429 => AiErrorCode.RateLimited,
        400 or 405 or 409 or 413 or 415 or 422 => AiErrorCode.InvalidRequest,
        >= 500 => AiErrorCode.ProviderUnavailable,
        _ => AiErrorCode.Unknown,
    };

    /// <summary>Parses a <c>Retry-After</c> header (delta-seconds or HTTP-date form).</summary>
    /// <param name="headers">The response headers.</param>
    /// <param name="timeProvider">The clock used for HTTP-date deltas (system time when omitted).</param>
    public static TimeSpan? ParseRetryAfter(HttpResponseHeaders headers, TimeProvider? timeProvider = null)
    {
        Guard.NotNull(headers);
        RetryConditionHeaderValue? retryAfter = headers.RetryAfter;
        if (retryAfter is null)
        {
            // Some providers send non-standard casing/values; fall back to raw parsing.
            if (headers.TryGetValues("retry-after", out IEnumerable<string>? values)
                && double.TryParse(values.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double rawSeconds)
                && rawSeconds >= 0)
            {
                return TimeSpan.FromSeconds(rawSeconds);
            }

            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta >= TimeSpan.Zero ? delta : null;
        }

        if (retryAfter.Date is { } date)
        {
            TimeSpan untilDate = date - (timeProvider ?? TimeProvider.System).GetUtcNow();
            return untilDate > TimeSpan.Zero ? untilDate : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>Truncates diagnostic payloads to 4 KB.</summary>
    /// <param name="body">The payload to truncate.</param>
    public static string? Truncate(string? body)
        => body is { Length: > MaxBodyLength } ? body[..MaxBodyLength] : body;
}
