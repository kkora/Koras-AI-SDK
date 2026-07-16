namespace Koras.AI;

/// <summary>
/// Controls whether and how the model may use the tools supplied in
/// <see cref="ChatOptions.Tools"/>.
/// </summary>
/// <param name="Value">The canonical choice value (<c>"auto"</c>, <c>"none"</c>, <c>"required"</c>, or a tool name prefixed with <c>"tool:"</c>).</param>
public readonly record struct ToolChoice(string Value)
{
    private const string ToolPrefix = "tool:";

    /// <summary>The model decides whether to call a tool (the default).</summary>
    public static ToolChoice Auto { get; } = new("auto");

    /// <summary>The model must not call tools.</summary>
    public static ToolChoice None { get; } = new("none");

    /// <summary>The model must call at least one tool.</summary>
    public static ToolChoice Required { get; } = new("required");

    /// <summary>Forces the model to call the named tool.</summary>
    /// <param name="name">The name of the tool that must be called.</param>
    public static ToolChoice Tool(string name) => new(ToolPrefix + Guard.NotNullOrWhiteSpace(name));

    /// <summary>When this choice forces a specific tool, its name; otherwise <see langword="null"/>.</summary>
    public string? RequiredToolName
        => Value?.StartsWith(ToolPrefix, StringComparison.Ordinal) is true ? Value[ToolPrefix.Length..] : null;

    /// <inheritdoc />
    public override string ToString() => Value;
}
