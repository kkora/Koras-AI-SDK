namespace Koras.AI;

/// <summary>
/// The provider-neutral classification of an AI operation failure. Every provider maps its
/// wire-level errors into this taxonomy, so consumers can handle failures identically across
/// providers. New values may be added in minor releases — always default-case switches.
/// </summary>
public enum AiErrorCode
{
    /// <summary>The failure could not be classified.</summary>
    Unknown = 0,

    /// <summary>Credentials are missing, malformed, or rejected (HTTP 401/403).</summary>
    Authentication,

    /// <summary>Authenticated, but not permitted to use the requested resource (organization, region, or model access).</summary>
    PermissionDenied,

    /// <summary>The requested model or deployment does not exist or is not available.</summary>
    ModelNotFound,

    /// <summary>The request was rejected as invalid (malformed parameters, context length exceeded).</summary>
    InvalidRequest,

    /// <summary>The provider's safety system blocked the input or the output.</summary>
    ContentFiltered,

    /// <summary>The provider throttled the request. <see cref="AiException.RetryAfter"/> carries the suggested wait when provided.</summary>
    RateLimited,

    /// <summary>The provider reported a server-side failure or is overloaded (HTTP 5xx).</summary>
    ProviderUnavailable,

    /// <summary>The request failed at the network layer (DNS, connect, socket).</summary>
    Network,

    /// <summary>The operation exceeded its configured timeout.</summary>
    Timeout,

    /// <summary>The operation was canceled. Used only inside aggregate reports; direct cancellation surfaces as <see cref="OperationCanceledException"/>.</summary>
    Canceled,

    /// <summary>The provider returned a success payload that could not be parsed, or structured output failed to deserialize.</summary>
    InvalidResponse,

    /// <summary>The capability is not supported by this provider (for example embeddings on Anthropic).</summary>
    NotSupported,

    /// <summary>A registered tool handler threw and the configured policy propagates tool failures.</summary>
    ToolExecutionFailed,

    /// <summary>The client is misconfigured (detected at call time rather than startup).</summary>
    Configuration,
}
