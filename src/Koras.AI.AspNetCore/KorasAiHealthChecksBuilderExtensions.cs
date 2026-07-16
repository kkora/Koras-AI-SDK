using Koras.AI;
using Koras.AI.AspNetCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers Koras.AI health checks.</summary>
public static class KorasAiHealthChecksBuilderExtensions
{
    /// <summary>
    /// Adds a health check that probes a Koras.AI client's provider through its lightweight
    /// probe endpoint (never a paid completion). Transient probe failures report
    /// <see cref="HealthStatus.Degraded"/>; terminal failures (for example bad credentials)
    /// report <paramref name="failureStatus"/>.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="clientName">The named client to probe, or <see langword="null"/> for the first registered provider client.</param>
    /// <param name="healthCheckName">The health check name (defaults to <c>koras-ai</c> or <c>koras-ai-{clientName}</c>).</param>
    /// <param name="failureStatus">The status reported for terminal failures (default <see cref="HealthStatus.Unhealthy"/>).</param>
    /// <param name="tags">Optional tags for filtering health-check endpoints.</param>
    public static IHealthChecksBuilder AddKorasAI(
        this IHealthChecksBuilder builder,
        string? clientName = null,
        string? healthCheckName = null,
        HealthStatus failureStatus = HealthStatus.Unhealthy,
        IEnumerable<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        string name = healthCheckName ?? (clientName is null ? "koras-ai" : $"koras-ai-{clientName}");

        return builder.Add(new HealthCheckRegistration(
            name,
            sp =>
            {
                var factory = sp.GetRequiredService<IChatClientFactory>();
                string resolvedClient = clientName ?? factory.ClientNames.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        "No Koras.AI clients are registered; call AddKorasAI with a provider before adding the health check.");
                return new KorasAiHealthCheck(factory, resolvedClient);
            },
            failureStatus,
            tags));
    }
}
