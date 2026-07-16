# Recipes: Production Configuration

The recommended baseline for running Koras.AI in production: bounded retries, timeouts,
fallback, readiness-wired health checks, OpenTelemetry export, and secrets kept out of
configuration files.

## The composition root

```csharp
using Koras.AI;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddKorasAI(ai =>
{
    ai.AddAzureOpenAI(builder.Configuration.GetSection("Koras:AI:AzureOpenAI"));
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));

    ai.AddFallback("resilient", "azure_openai", "openai").AsDefault();

    ai.UseRetry(r =>
    {
        r.MaxAttempts = 3;                              // total, including the first
        r.BaseDelay = TimeSpan.FromSeconds(1);          // exponential, full jitter
        r.MaxDelay = TimeSpan.FromSeconds(20);
        r.AttemptTimeout = TimeSpan.FromSeconds(45);    // per attempt; timeouts are transient
        r.HonorRetryAfter = true;                       // provider hints override backoff
    });

    // EnableSensitiveData defaults to false. Leave it that way in production —
    // prompts and completions are user data.
});

builder.Services.AddHealthChecks()
    .AddKorasAI("azure_openai", tags: ["ready"]);

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddKorasAI().AddOtlpExporter())
    .WithMetrics(m => m.AddKorasAI().AddOtlpExporter());

WebApplication app = builder.Build();   // ValidateOnStart: bad options fail here, not mid-request

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();
```

## Retry and timeout tuning

| Setting | Default | Production guidance |
|---|---|---|
| `MaxAttempts` | 3 | Keep at 2–3 when a fallback chain exists — retries multiply across candidates. |
| `BaseDelay` / `MaxDelay` | 1 s / 30 s | Lower `MaxDelay` (≈20 s) for interactive endpoints; keep defaults for batch. |
| `AttemptTimeout` | 100 s | Set below your caller-facing timeout; 30–60 s suits chat, less for short prompts. |
| `HonorRetryAfter` | `true` | Leave on — providers know their own backpressure. |

Only failures with `AiException.IsTransient == true` retry. Streaming requests retry only if
they fail before the first token.

## Fallback

Order candidates by preference (primary region/account first). Terminal errors — bad key,
invalid request, content filter policy — do not fail over; only transient failures do. Watch
the `koras.ai.client.fallbacks` counter: a nonzero steady state means your primary is unwell.

## Health checks and readiness

`AddKorasAI("azure_openai", tags: ["ready"])` probes the provider's lightweight endpoint
(deployment metadata, model list, or version — never a paid completion). Wire it to the
readiness probe only; liveness should not depend on an external provider. In Kubernetes,
point `readinessProbe` at `/health/ready` so a provider outage drains traffic instead of
crash-looping pods.

## Key management

- Ship keys through environment variables (`Koras__AI__AzureOpenAI__ApiKey`) or a vault
  (Azure Key Vault configuration provider, Kubernetes Secrets) — see
  [environment variables](../configuration/environment-variables.md).
- Never put keys in `appsettings.json` or source control — see
  [appsettings guidance](../configuration/appsettings.md).
- Startup validation fails fast with a message naming the missing key:
  `Koras.AI OpenAI client 'openai': ApiKey is required...` — see
  [validation](../configuration/validation.md).
- Keys never appear in logs, exceptions, or telemetry; `ProviderErrorBody` is scrubbed.

## Sensitive data stays off

`KorasAiTelemetryOptions.EnableSensitiveData` gates prompt/completion capture at `Trace`
level and defaults to `false`. Do not enable it in production — treat it as a local-debugging
switch only. See [logging](../troubleshooting/logging.md).

## The quota-429 nuance

Providers send HTTP 429 for two very different conditions:

- **Burst rate limiting** (e.g. OpenAI `rate_limit_exceeded`) → `AiErrorCode.RateLimited`
  with `IsTransient = true`: retried and failed over automatically.
- **Exhausted quota** (OpenAI `insufficient_quota`) → `AiErrorCode.RateLimited` with
  `IsTransient = false`: retrying cannot help, so the SDK does not retry; fix billing/quota.

Alert on the two differently: transient 429s are backpressure; non-transient 429s are an
account problem. Details in [provider errors](../troubleshooting/provider-errors.md).

## Production checklist

- [ ] Keys from env/vault only; `ValidateOnStart` passing in a staging boot.
- [ ] Retry bounded (`MaxAttempts` ≤ 3) and `AttemptTimeout` below the caller timeout.
- [ ] Fallback chain ordered, with alerts on `koras.ai.client.fallbacks`.
- [ ] Readiness probe wired to `AddKorasAI` health check; liveness independent of providers.
- [ ] OTel traces + metrics exported; dashboards on duration histogram and token counters.
- [ ] `EnableSensitiveData` unset (false) everywhere outside local development.
