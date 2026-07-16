namespace Koras.AI;

/// <summary>
/// Per-request generation parameters. All values are optional; unset values use the
/// provider's defaults. Instances are immutable and safe to reuse across requests.
/// </summary>
public sealed class ChatOptions
{
    /// <summary>Sampling temperature (typically 0.0–2.0; lower is more deterministic).</summary>
    public double? Temperature { get; init; }

    /// <summary>Nucleus-sampling probability mass (0.0–1.0). Prefer adjusting either this or <see cref="Temperature"/>, not both.</summary>
    public double? TopP { get; init; }

    /// <summary>The maximum number of tokens the model may generate for this response.</summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>Sequences that stop generation when produced.</summary>
    public IReadOnlyList<string>? StopSequences { get; init; }

    /// <summary>The required output shape (text, JSON, or schema-constrained JSON).</summary>
    public ChatResponseFormat? ResponseFormat { get; init; }

    /// <summary>Tools the model may call during this request.</summary>
    public IReadOnlyList<AiTool>? Tools { get; init; }

    /// <summary>Controls whether/how the model may use <see cref="Tools"/>. Defaults to auto when tools are present.</summary>
    public ToolChoice? ToolChoice { get; init; }

    /// <summary>
    /// Provider-specific request fields merged into the wire request — the escape hatch for
    /// capabilities the abstraction does not model. Keys and value shapes are documented per
    /// provider; unknown keys are passed through verbatim.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }
}
