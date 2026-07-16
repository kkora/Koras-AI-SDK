namespace Koras.AI;

/// <summary>
/// A provider-neutral embedding generation client. Implementations are thread-safe and
/// intended for singleton use.
/// </summary>
public interface IEmbeddingClient
{
    /// <summary>The stable, lower-case provider identifier (for example <c>"openai"</c>).</summary>
    string ProviderName { get; }

    /// <summary>Generates one embedding vector per input value.</summary>
    /// <param name="request">The inputs and model selection.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The embeddings, ordered by input index.</returns>
    /// <exception cref="AiException">
    /// The operation failed; providers without an embeddings API throw with
    /// <see cref="AiErrorCode.NotSupported"/>.
    /// </exception>
    Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
}
