namespace Koras.AI;

/// <summary>Well-known names of the SDK's telemetry sources, for wiring into telemetry pipelines.</summary>
public static class KorasAiTelemetry
{
    /// <summary>The name of the SDK's <see cref="System.Diagnostics.ActivitySource"/>.</summary>
    public const string ActivitySourceName = "Koras.AI";

    /// <summary>The name of the SDK's <see cref="System.Diagnostics.Metrics.Meter"/>.</summary>
    public const string MeterName = "Koras.AI";
}
