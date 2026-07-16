namespace Koras.AI;

/// <summary>
/// An optional capability implemented by provider clients that can cheaply verify
/// connectivity and credentials (for example by listing models) — used by health checks.
/// Probes never invoke paid model inference.
/// </summary>
public interface IProviderHealthProbe
{
    /// <summary>Verifies the provider is reachable and credentials are accepted.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <exception cref="AiException">The probe failed; see <see cref="AiException.Code"/>.</exception>
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
