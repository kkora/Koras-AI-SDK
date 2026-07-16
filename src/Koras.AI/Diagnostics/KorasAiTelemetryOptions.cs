namespace Koras.AI;

/// <summary>Configures the SDK's built-in logging and tracing behavior.</summary>
public sealed class KorasAiTelemetryOptions
{
    /// <summary>
    /// When enabled, message content may be logged at Trace level for local debugging.
    /// <b>Default false</b> — prompts and completions frequently contain personal or
    /// confidential data; never enable in production.
    /// </summary>
    public bool EnableSensitiveData { get; set; }
}
