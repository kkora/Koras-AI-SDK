namespace Koras.AI;

/// <summary>How the tool-invocation loop reacts when a tool handler throws.</summary>
public enum ToolErrorBehavior
{
    /// <summary>Report the failure to the model as the tool result and let it recover (the default).</summary>
    ReturnToModel = 0,

    /// <summary>Propagate the failure as an <see cref="AiException"/> with <see cref="AiErrorCode.ToolExecutionFailed"/>.</summary>
    Throw = 1,
}

/// <summary>Configures the automatic tool-invocation loop added by <see cref="KorasAiBuilder.UseToolInvocation"/>.</summary>
public sealed class ToolInvocationOptions
{
    private int _maxIterations = 8;

    /// <summary>
    /// The maximum number of model round-trips per request (default 8). Exceeding the bound
    /// throws <see cref="AiException"/> with <see cref="AiErrorCode.ToolExecutionFailed"/> —
    /// the guard against tool-call loops that never converge.
    /// </summary>
    public int MaxIterations
    {
        get => _maxIterations;
        set => _maxIterations = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), "MaxIterations must be at least 1.");
    }

    /// <summary>What happens when a tool handler throws (default <see cref="ToolErrorBehavior.ReturnToModel"/>).</summary>
    public ToolErrorBehavior ErrorBehavior { get; set; } = ToolErrorBehavior.ReturnToModel;
}
