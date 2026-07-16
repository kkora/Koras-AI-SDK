using System.Diagnostics.CodeAnalysis;

namespace Koras.AI;

/// <summary>A request to generate embedding vectors for one or more input texts.</summary>
public sealed class EmbeddingRequest
{
    /// <summary>Initializes an empty request for object-initializer construction.</summary>
    public EmbeddingRequest()
    {
    }

    /// <summary>Initializes a request for the given input values.</summary>
    /// <param name="values">The texts to embed.</param>
    [SetsRequiredMembers]
    public EmbeddingRequest(params string[] values)
    {
        Values = Guard.NotNull(values);
    }

    /// <summary>The texts to embed, in order. The response contains one vector per value.</summary>
    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>The embedding model to use, or <see langword="null"/> for the client's configured default.</summary>
    public string? Model { get; init; }

    /// <summary>The requested vector dimensionality, for models that support shortening.</summary>
    public int? Dimensions { get; init; }
}

/// <summary>A single embedding vector.</summary>
public sealed class Embedding
{
    /// <summary>Initializes an embedding.</summary>
    /// <param name="vector">The embedding vector.</param>
    /// <param name="index">The zero-based index of the input value this vector corresponds to.</param>
    public Embedding(ReadOnlyMemory<float> vector, int index)
    {
        Vector = vector;
        Index = index;
    }

    /// <summary>The embedding vector.</summary>
    public ReadOnlyMemory<float> Vector { get; }

    /// <summary>The zero-based index of the input value this vector corresponds to.</summary>
    public int Index { get; }
}

/// <summary>The result of an embedding generation request.</summary>
public sealed class EmbeddingResponse
{
    /// <summary>The generated embeddings, ordered by input index.</summary>
    public required IReadOnlyList<Embedding> Embeddings { get; init; }

    /// <summary>The provider that served the request.</summary>
    public required string Provider { get; init; }

    /// <summary>The model that produced the embeddings, as reported by the provider.</summary>
    public string? Model { get; init; }

    /// <summary>Token consumption, when the provider reports it.</summary>
    public TokenUsage Usage { get; init; }
}
