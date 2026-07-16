# Secure Configuration

How to configure Koras.AI so credentials stay secret and traffic stays private. Companion to
the [threat model](threat-model.md).

## API key management

Keys are accepted **only** through provider options (`OpenAIOptions.ApiKey`,
`AnthropicOptions.ApiKey`, …). There is no environment-variable magic inside the SDK — you
choose the source and bind it. The rules:

| Environment | Do | Don't |
|---|---|---|
| Development | `dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."` | Put keys in `appsettings.json` or `appsettings.Development.json` |
| Production | Environment variables (`Koras__AI__OpenAI__ApiKey`) or a vault (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault) surfaced through `IConfiguration` | Hard-code keys in source, bake them into images, or commit them anywhere |

```csharp
// Binds ApiKey/Endpoint/DefaultModel from configuration — the key never appears in code.
builder.Services.AddKorasAI()
    .AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
```

A missing key fails fast: options validation runs at startup (`ValidateOnStart`) with the
message *"ApiKey is required. Provide it via configuration or user secrets — never source
code."* Keys are sent only in authentication headers (`Authorization: Bearer` for OpenAI,
`api-key` for Azure OpenAI, `x-api-key` for Anthropic, `x-goog-api-key` for Gemini) — never in
query strings, so they never land in URL logs or proxies' access logs.

## HTTPS enforcement

Every provider registration validates its endpoint at startup:

- Remote endpoints **must** use `https://`. An `http://` remote endpoint fails app startup
  with an `OptionsValidationException`.
- Loopback addresses (`localhost`, `127.0.0.1`, `[::1]`) are exempt, so a local Ollama at
  `http://localhost:11434` works without ceremony.

Do not work around this by terminating TLS in a local sidecar unless that sidecar itself is on
loopback and the upstream hop is encrypted.

## Least-privilege API keys

Create keys scoped as narrowly as your provider allows: per-application keys, model-restricted
keys, and spend limits where offered (OpenAI project keys, Azure OpenAI per-resource keys,
Anthropic workspace keys). Rotate on personnel changes and on any suspected exposure — the SDK
reads the key from options on each request, so rotation via configuration reload requires no
code change.

## Endpoint override cautions

`Endpoint` exists to point at gateways, proxies, and regional deployments — it is deploy-time
operator configuration. **Never** populate it from user input (request headers, form fields,
tenant records editable by end users): that converts your server into an SSRF proxy that also
forwards your API key to the attacker's endpoint. See the residual-risk note in the
[threat model](threat-model.md).

## `EnableSensitiveData`

`KorasAiTelemetryOptions.EnableSensitiveData` (default `false`) gates all content capture.
Prompts and completions are logged only when it is `true` **and** the logger has `Trace`
enabled — both conditions, deliberately, so production `Information`-level logging can never
leak content. Treat it as a local-debugging switch:

- Never enable it in production.
- If you must enable it in a shared environment, ensure the log sink itself is access
  controlled and has a short retention window.

## Multi-tenant guidance

- Register **one named client per tenant** (`ai.AddOpenAI("tenant-a", ...)`) so each tenant's
  key, endpoint, and model stay isolated; resolve by name via `IChatClientFactory`.
- **Never share conversation state across tenants.** The SDK holds no conversation state
  itself — whatever history store you build must partition by tenant.
- Include the tenant in your own log scopes; the SDK tags telemetry with the client name
  (`koras.ai.client.name`), which gives you per-tenant metrics for free when clients map
  one-to-one to tenants.
- Apply per-tenant rate limiting above the SDK; provider 429s are retried per the shared
  retry policy and one noisy tenant can consume another's provider quota otherwise.

## Startup checklist

- [ ] Keys come from user secrets (dev) or env vars/vault (prod) — never `appsettings` or code.
- [ ] All remote endpoints are HTTPS (validated automatically, but check your config values).
- [ ] `EnableSensitiveData` is absent or `false` in every non-local configuration.
- [ ] Keys are least-privilege and rotation is scripted.
- [ ] Multi-tenant apps use named clients per tenant.

See also the full [security checklist](security-checklist.md).
