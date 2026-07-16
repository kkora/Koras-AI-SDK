namespace Koras.AI.OpenAI;

/// <summary>
/// Configures the OpenAI provider. Bind from configuration (section
/// <c>Koras:AI:OpenAI</c> by convention) or configure in code. The API key must come from a
/// secret source (user secrets, environment variables, a vault) — never source code.
/// </summary>
public sealed class OpenAIOptions
{
    /// <summary>The OpenAI API key. Required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The API base endpoint (default <c>https://api.openai.com/v1/</c>). Point at an
    /// OpenAI-compatible gateway to reuse this provider against proxies. Must be HTTPS except
    /// for loopback addresses.
    /// </summary>
    public Uri Endpoint { get; set; } = new("https://api.openai.com/v1/");

    /// <summary>The model used when a request does not specify one (for example <c>"gpt-4o-mini"</c>).</summary>
    public string? DefaultModel { get; set; }

    /// <summary>The embedding model used when a request does not specify one (for example <c>"text-embedding-3-small"</c>).</summary>
    public string? DefaultEmbeddingModel { get; set; }

    /// <summary>The OpenAI organization id sent as <c>OpenAI-Organization</c>, when set.</summary>
    public string? Organization { get; set; }
}
