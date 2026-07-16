namespace Koras.AI;

/// <summary>
/// Token consumption reported by a provider for a single operation. Providers that do not
/// report usage leave the counts at zero.
/// </summary>
/// <param name="InputTokens">Tokens consumed by the request (prompt) side.</param>
/// <param name="OutputTokens">Tokens produced by the model.</param>
public readonly record struct TokenUsage(int InputTokens, int OutputTokens)
{
    /// <summary>The sum of <see cref="InputTokens"/> and <see cref="OutputTokens"/>.</summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>Adds two usage measurements component-wise (useful when aggregating tool-loop iterations).</summary>
    /// <param name="left">The first usage value.</param>
    /// <param name="right">The second usage value.</param>
    public static TokenUsage operator +(TokenUsage left, TokenUsage right)
        => new(left.InputTokens + right.InputTokens, left.OutputTokens + right.OutputTokens);
}
