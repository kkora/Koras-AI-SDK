# Health Checks

## Overview

`Koras.AI.AspNetCore` plugs provider connectivity into ASP.NET Core health checks. The
`IHealthChecksBuilder.AddKorasAI(...)` extension registers a check that probes a named
Koras.AI client through its provider's `IProviderHealthProbe` — a cheap liveness endpoint
(models list or version), never a paid completion. Transient probe failures report
`Degraded`; terminal failures (for example bad credentials) report the configured
`failureStatus` (default `Unhealthy`).

## When to use it

Wire it into readiness/liveness endpoints of any service whose core function depends on a
model provider, and into orchestrator probes and dashboards.

## When not to use it

Do not use provider probes as billing or quota checks — they verify reachability and
authentication, not model behavior. For end-to-end verification run a scheduled synthetic
completion instead.

## Required packages

- `Koras.AI.AspNetCore` (plus `Koras.AI` and provider packages).

## Basic usage

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o => { o.ApiKey = cfg["OpenAI:ApiKey"]; o.DefaultModel = "gpt-4o-mini"; });
});

builder.Services.AddHealthChecks().AddKorasAI();

var app = builder.Build();
app.MapHealthChecks("/health");
```

With no `clientName`, the first registered provider client is probed and the check is named
`koras-ai`.

## Advanced configuration

```csharp
builder.Services.AddHealthChecks()
    .AddKorasAI(clientName: "openai", tags: ["ready", "ai"])
    .AddKorasAI(
        clientName: "ollama",
        healthCheckName: "local-model",
        failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
        tags: ["ready"]);
```

- `clientName` — the named client to probe (default: first registered).
- `healthCheckName` — defaults to `koras-ai` or `koras-ai-{clientName}`.
- `failureStatus` — status for terminal failures (default `Unhealthy`).
- `tags` — filter which checks a given endpoint evaluates.

## Probe endpoints per provider

| Provider | Probe |
|---|---|
| OpenAI | `GET {Endpoint}models` |
| Azure OpenAI | authenticated GET against the resource (inherited `models` route, `api-key` header) |
| Anthropic | `GET {Endpoint}v1/models?limit=1` |
| Gemini | `GET {Endpoint}models?pageSize=1` |
| Ollama | `GET {Endpoint}api/version` |

A client whose provider does not implement `IProviderHealthProbe` reports `Healthy` with an
explanatory description ("connectivity not verified") — absence of a probe is not treated as
failure. The check discovers the probe through decorator stacks via
`DelegatingChatClient.GetService(typeof(IProviderHealthProbe))`.

## Dependency-injection usage

The check resolves `IChatClientFactory` lazily; register `AddKorasAI` (the service-collection
one) with at least one provider before the health check runs, or the check factory throws
`InvalidOperationException` telling you no clients are registered.

## Error handling

Probe failures are `AiException`s and are classified by `IsTransient`:

- transient (`RateLimited`, `ProviderUnavailable`, `Network`, `Timeout`) → `Degraded`
- terminal (for example `Authentication`) → `failureStatus`
- unknown `clientName` → `failureStatus` with an explanatory description

The exception is attached to the `HealthCheckResult` for logging; descriptions include the
client name, provider, and error code — never credentials.

## Cancellation

The health-check framework's `CancellationToken` flows into `ProbeAsync`; slow probes are
cancelled by the framework's timeout rather than hanging the endpoint.

## Logging and telemetry

Degraded probes log a `Warning` in the `Koras.AI.*` categories. Health endpoints themselves
are standard ASP.NET Core; combine with [telemetry](telemetry.md) counters for trend data.

## Security considerations

Probes send real credentials to the provider — the same key material as production traffic.
Expose health endpoints privately or behind authorization; a public detailed endpoint can leak
which providers and clients you run.

## Performance considerations

Probes are network calls on every evaluation. Rate-limit health endpoint polling and use
distinct tags so liveness (no probes) and readiness (with probes) can be evaluated at
different frequencies.

## Thread safety

The health check holds no mutable state; concurrent evaluations are safe.

## Testing applications using this feature

Register a client whose provider implements `IProviderHealthProbe` in a test host and assert
the mapped status:

```csharp
private sealed class ProbeClient(Exception? failure) : IChatClient, IProviderHealthProbe
{
    public string ProviderName => "fake";
    public Task ProbeAsync(CancellationToken cancellationToken = default)
        => failure is null ? Task.CompletedTask : Task.FromException(failure);
    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
```

Pass `new AiException("throttled", AiErrorCode.RateLimited)` and expect `Degraded`; pass an
`Authentication` failure and expect `Unhealthy`.

## Common mistakes

- Probing in tight liveness loops and burning provider rate limits.
- Treating `Degraded` as down — it signals a transient condition that retry/fallback likely
  absorbs.
- Adding the health check without any registered Koras.AI client.
- Expecting the probe to validate `DefaultModel`; it checks reachability and auth, not model
  availability for your deployment (except where the probe route is model-scoped).

## Related features

- [Dependency injection](dependency-injection.md)
- [Provider fallback](provider-fallback.md)
- [Telemetry](telemetry.md)
