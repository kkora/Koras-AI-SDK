# Health Checks Guide

`Koras.AI.AspNetCore` plugs provider reachability into ASP.NET Core's standard health-check
system. The check calls the provider's **cheap probe endpoint — never a paid completion** —
so it is safe to run on every readiness poll.

```sh
dotnet add package Koras.AI.AspNetCore -v 0.1.0-preview.1
```

## Basic usage

```csharp
builder.Services.AddHealthChecks().AddKorasAI();

var app = builder.Build();
app.MapHealthChecks("/health");
```

With no arguments, the check probes the **first registered** provider client under the
health-check name `koras-ai`.

## Options

```csharp
builder.Services.AddHealthChecks().AddKorasAI(
    clientName: "openai",             // which named client to probe (null = first registered)
    healthCheckName: "ai-openai",     // registration name (default: koras-ai or koras-ai-{clientName})
    failureStatus: HealthStatus.Degraded,   // status for terminal failures (default: Unhealthy)
    tags: ["ready"]);                 // tags for endpoint filtering
```

Call it multiple times to probe several clients independently:

```csharp
builder.Services.AddHealthChecks()
    .AddKorasAI("openai", tags: ["ready"])       // → check "koras-ai-openai"
    .AddKorasAI("ollama", tags: ["ready"]);      // → check "koras-ai-ollama"
```

## Degraded vs. Unhealthy

The check distinguishes failures by `AiException.IsTransient`:

| Probe outcome | Reported status |
|---|---|
| Probe succeeds | `Healthy` — "client 'x' (provider) is reachable" |
| Transient failure (network blip, 5xx, rate limit, timeout) | `Degraded` |
| Terminal failure (bad credentials, permission denied) | `failureStatus` (default `Unhealthy`) |
| Provider exposes no probe | `Healthy`, with a description noting connectivity was not verified |
| Unknown client name | `failureStatus` |

The reasoning: a transient blip should not make an orchestrator restart or de-route your
pod — the SDK's retry/fallback already absorbs it — while a rejected API key is a real
misconfiguration worth failing over. If even terminal AI failures should not take your app
out of rotation (AI is a non-critical feature), pass
`failureStatus: HealthStatus.Degraded`.

## Probe endpoints per provider

Providers implement the optional `IProviderHealthProbe` capability with their cheapest
authenticated endpoint:

| Provider | Probe |
|---|---|
| OpenAI (and gateways) | `GET /models` |
| Azure OpenAI | models/deployments listing on the resource |
| Anthropic | `GET /v1/models?limit=1` |
| Gemini | `GET /models?pageSize=1` |
| Ollama | `GET /api/version` (also verifies the server is running at all) |

Because the OpenAI/Anthropic/Gemini probes are authenticated, they genuinely validate the
API key — a rotated-out key flips the check to `Unhealthy` without spending a single token.

## Readiness tags

The standard ASP.NET Core pattern — tag the AI checks and split liveness from readiness:

```csharp
builder.Services.AddHealthChecks().AddKorasAI("ollama", tags: ["ready"]);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,                          // liveness: process is up, no dependencies
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),       // readiness: includes the AI probe
});
```

(`HealthCheckOptions` is `Microsoft.AspNetCore.Diagnostics.HealthChecks`.) This is the
setup used by [`samples/WebApi.Sample`](../../samples/WebApi.Sample/Program.cs), which
probes only the local Ollama fallback for readiness — hosted providers may come and go, but
the service is "ready" as long as its always-available candidate responds.

## Sample response

With the default writer, `/health` returns the aggregate status text; richer writers (for
example `UIResponseWriter` from AspNetCore.HealthChecks.UI.Client) surface each entry:

```json
{
  "status": "Degraded",
  "entries": {
    "koras-ai-openai": { "status": "Degraded", "description": "Koras.AI client 'openai' (openai) probe failed: RateLimited." },
    "koras-ai-ollama": { "status": "Healthy",  "description": "Koras.AI client 'ollama' (ollama) is reachable." }
  }
}
```

## Related

- [ASP.NET Core guide](aspnet-core.md) — the sample this configuration comes from.
- [Observability](../architecture/observability.md) — health checks alongside logs, traces, metrics.
