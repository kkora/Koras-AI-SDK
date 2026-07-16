namespace Koras.AI;

/// <summary>
/// A provider-neutral chat completion client. Implementations are thread-safe and intended
/// for singleton use. Failures surface as <see cref="AiException"/>; caller cancellation
/// surfaces as <see cref="OperationCanceledException"/>.
/// </summary>
public interface IChatClient
{
    /// <summary>The stable, lower-case provider identifier (for example <c>"openai"</c>).</summary>
    string ProviderName { get; }

    /// <summary>Sends the conversation to the model and returns its complete response.</summary>
    /// <param name="request">The conversation and generation options.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The model's response with content, finish reason, and usage.</returns>
    /// <exception cref="AiException">The operation failed; see <see cref="AiException.Code"/>.</exception>
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the conversation to the model and streams its response incrementally. The
    /// network request starts when enumeration starts; disposing the enumerator releases the
    /// connection.
    /// </summary>
    /// <param name="request">The conversation and generation options.</param>
    /// <param name="cancellationToken">Cancels the stream.</param>
    /// <returns>Incremental updates ending with a terminal update carrying the finish reason.</returns>
    /// <exception cref="AiException">The operation failed before or during streaming; see <see cref="AiException.Code"/>.</exception>
    IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
