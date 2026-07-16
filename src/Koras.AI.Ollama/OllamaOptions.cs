namespace Koras.AI.Ollama;

/// <summary>
/// Configures the Ollama provider (local or self-hosted). Bind from configuration (section
/// <c>Koras:AI:Ollama</c> by convention) or configure in code. No API key is required.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>The Ollama endpoint (default <c>http://localhost:11434/</c>).</summary>
    public Uri Endpoint { get; set; } = new("http://localhost:11434/");

    /// <summary>The model used when a request does not specify one (for example <c>"llama3.2"</c>).</summary>
    public string? DefaultModel { get; set; }

    /// <summary>The embedding model used when a request does not specify one (for example <c>"nomic-embed-text"</c>).</summary>
    public string? DefaultEmbeddingModel { get; set; }
}
