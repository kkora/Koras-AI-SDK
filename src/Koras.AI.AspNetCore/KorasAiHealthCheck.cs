using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Koras.AI.AspNetCore;

/// <summary>
/// A health check that verifies a named Koras.AI client can reach its provider, using the
/// provider's lightweight probe (models/version endpoints — never a paid completion). Clients
/// whose provider does not implement <see cref="IProviderHealthProbe"/> report
/// <see cref="HealthStatus.Healthy"/> with an explanatory description.
/// </summary>
internal sealed class KorasAiHealthCheck(IChatClientFactory factory, string clientName) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        IChatClient client;
        try
        {
            client = factory.GetChatClient(clientName);
        }
        catch (InvalidOperationException ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"Unknown Koras.AI client '{clientName}'.", ex);
        }

        IProviderHealthProbe? probe = client as IProviderHealthProbe
            ?? (client as DelegatingChatClient)?.GetService(typeof(IProviderHealthProbe)) as IProviderHealthProbe;

        if (probe is null)
        {
            return HealthCheckResult.Healthy($"Koras.AI client '{clientName}' does not expose a health probe; connectivity not verified.");
        }

        try
        {
            await probe.ProbeAsync(cancellationToken).ConfigureAwait(false);
            return HealthCheckResult.Healthy($"Koras.AI client '{clientName}' ({client.ProviderName}) is reachable.");
        }
        catch (AiException ex)
        {
            string description = $"Koras.AI client '{clientName}' ({client.ProviderName}) probe failed: {ex.Code}.";
            return ex.IsTransient
                ? HealthCheckResult.Degraded(description, ex)
                : new HealthCheckResult(context.Registration.FailureStatus, description, ex);
        }
    }
}
