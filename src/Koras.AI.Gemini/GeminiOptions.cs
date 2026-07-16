namespace Koras.AI.Gemini;

/// <summary>
/// Configures the Google Gemini provider. Bind from configuration (section
/// <c>Koras:AI:Gemini</c> by convention) or configure in code. The API key must come from a
/// secret source — never source code. The key is sent as the <c>x-goog-api-key</c> header,
/// never in the query string.
/// </summary>
public sealed class GeminiOptions
{
    /// <summary>The Gemini API key. Required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The API base endpoint (default <c>https://generativelanguage.googleapis.com/v1beta/</c>).</summary>
    public Uri Endpoint { get; set; } = new("https://generativelanguage.googleapis.com/v1beta/");

    /// <summary>The model used when a request does not specify one (for example <c>"gemini-2.0-flash"</c>).</summary>
    public string? DefaultModel { get; set; }

    /// <summary>The embedding model used when a request does not specify one (for example <c>"text-embedding-004"</c>).</summary>
    public string? DefaultEmbeddingModel { get; set; }
}
