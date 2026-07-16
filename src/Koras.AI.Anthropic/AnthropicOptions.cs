namespace Koras.AI.Anthropic;

/// <summary>
/// Configures the Anthropic (Claude) provider. Bind from configuration (section
/// <c>Koras:AI:Anthropic</c> by convention) or configure in code. The API key must come from a
/// secret source — never source code.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>The Anthropic API key, sent as the <c>x-api-key</c> header. Required.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The API base endpoint (default <c>https://api.anthropic.com/</c>).</summary>
    public Uri Endpoint { get; set; } = new("https://api.anthropic.com/");

    /// <summary>The model used when a request does not specify one (for example <c>"claude-sonnet-4-5"</c>).</summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// The <c>max_tokens</c> value used when a request does not set
    /// <see cref="ChatOptions.MaxOutputTokens"/> (default 4096). The Anthropic Messages API
    /// requires an explicit output-token limit on every request.
    /// </summary>
    public int DefaultMaxOutputTokens { get; set; } = 4096;

    /// <summary>The <c>anthropic-version</c> header value (default <c>2023-06-01</c>).</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";
}
