using Koras.AI;

namespace OpenTelemetry.Trace
{
    /// <summary>Registers Koras.AI tracing with OpenTelemetry.</summary>
    public static class KorasAiTracerProviderBuilderExtensions
    {
        /// <summary>Subscribes the OpenTelemetry tracer to the <c>Koras.AI</c> activity source (GenAI semantic conventions).</summary>
        /// <param name="builder">The tracer provider builder.</param>
        public static TracerProviderBuilder AddKorasAI(this TracerProviderBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddSource(KorasAiTelemetry.ActivitySourceName);
        }
    }
}

namespace OpenTelemetry.Metrics
{
    /// <summary>Registers Koras.AI metrics with OpenTelemetry.</summary>
    public static class KorasAiMeterProviderBuilderExtensions
    {
        /// <summary>Subscribes the OpenTelemetry meter provider to the <c>Koras.AI</c> meter (durations, token usage, retries, fallbacks).</summary>
        /// <param name="builder">The meter provider builder.</param>
        public static MeterProviderBuilder AddKorasAI(this MeterProviderBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.AddMeter(KorasAiTelemetry.MeterName);
        }
    }
}
