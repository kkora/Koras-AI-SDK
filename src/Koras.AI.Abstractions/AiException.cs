namespace Koras.AI;

/// <summary>
/// The single exception type thrown by Koras.AI clients for AI operation failures, carrying a
/// provider-neutral <see cref="AiErrorCode"/> plus normalized diagnostics. Caller cancellation
/// is the exception to the rule: it surfaces as <see cref="OperationCanceledException"/> per
/// the standard .NET contract.
/// </summary>
/// <remarks>
/// Diagnostic members never contain credentials: providers scrub authentication material
/// before attaching <see cref="ProviderErrorBody"/>.
/// </remarks>
public class AiException : Exception
{
    private readonly bool? _isTransient;

    /// <summary>Initializes the exception.</summary>
    /// <param name="message">A human-readable description of the failure.</param>
    /// <param name="code">The provider-neutral failure classification.</param>
    /// <param name="innerException">The underlying exception, when one triggered this failure.</param>
    public AiException(string message, AiErrorCode code = AiErrorCode.Unknown, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>The provider-neutral failure classification.</summary>
    public AiErrorCode Code { get; }

    /// <summary>The provider that produced the failure (for example <c>"openai"</c>), or <see langword="null"/> before dispatch.</summary>
    public string? Provider { get; init; }

    /// <summary>The HTTP status code, when the failure came from an HTTP response.</summary>
    public int? StatusCode { get; init; }

    /// <summary>The provider's suggested wait before retrying, parsed from <c>Retry-After</c> or an equivalent hint.</summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>The provider's raw error payload, truncated to 4 KB and scrubbed of credentials. May be <see langword="null"/>.</summary>
    public string? ProviderErrorBody { get; init; }

    /// <summary>The provider's request identifier, useful when contacting provider support.</summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Whether retrying the operation may succeed. Defaults from <see cref="Code"/>
    /// (<see cref="AiErrorCode.RateLimited"/>, <see cref="AiErrorCode.ProviderUnavailable"/>,
    /// <see cref="AiErrorCode.Network"/>, and <see cref="AiErrorCode.Timeout"/> are transient)
    /// and can be overridden per instance — for example a quota-exhausted 429 is not transient.
    /// Retry and fallback decorators consult only this flag.
    /// </summary>
    public bool IsTransient
    {
        get => _isTransient ?? Code is AiErrorCode.RateLimited
            or AiErrorCode.ProviderUnavailable
            or AiErrorCode.Network
            or AiErrorCode.Timeout;
        init => _isTransient = value;
    }
}
