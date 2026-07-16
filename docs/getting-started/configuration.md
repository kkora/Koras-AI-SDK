# Configuration

Provider clients are configured through standard .NET options types (`OpenAIOptions`,
`OllamaOptions`, …). You can set them in code, bind them from configuration, or mix both.

## Two ways to configure

**In code** — a configure delegate:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["Koras:AI:OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
});
```

**From configuration** — pass an `IConfiguration` section and it binds automatically:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI"));
});
```

## The `Koras:AI:*` section convention

By convention each provider binds from `Koras:AI:{Provider}`:

```json
{
  "Koras": {
    "AI": {
      "OpenAI":  { "DefaultModel": "gpt-4o-mini", "DefaultEmbeddingModel": "text-embedding-3-small" },
      "Ollama":  { "Endpoint": "http://localhost:11434/", "DefaultModel": "llama3.2" },
      "Anthropic": { "DefaultModel": "claude-sonnet-4-5" }
    }
  }
}
```

The convention is exactly that — a convention. Any section name works; the strings above are
what the samples and documentation use throughout.

## Secrets: never in appsettings.json

API keys must come from a secret source. The binder does not care where a value originates,
so all of these fill `Koras:AI:OpenAI:ApiKey` identically:

**User secrets (local development):**

```sh
dotnet user-secrets init
dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."
```

**Environment variables (containers, CI):** `:` becomes `__`:

```sh
export Koras__AI__OpenAI__ApiKey="sk-..."
```

**A vault (production):** register the vault's configuration provider (for example Azure
Key Vault via `AddAzureKeyVault`) and store the secret under the same key path. Because
secrets merge into the same section, `appsettings.json` holds the non-secret parts
(model, endpoint) and the secret arrives from wherever the environment provides it.

Diagnostics never leak keys: `AiException.ProviderErrorBody` is scrubbed of credentials, and
message content is never logged unless you opt in ([logging](../guides/logging.md)).

## Startup validation

Every provider registration attaches validation with `ValidateOnStart()`. Missing or invalid
required values fail **when the host starts**, not on the first request, with a message that
names the client and the fix:

```text
Microsoft.Extensions.Options.OptionsValidationException:
Koras.AI OpenAI client 'openai': ApiKey is required. Provide it via configuration or
user secrets — never source code.
```

Validated rules for OpenAI, for example:

- `ApiKey` is required.
- `Endpoint` must not be null.
- `Endpoint` must use HTTPS (plain HTTP is allowed only for loopback addresses, so local
  gateways still work).

Other providers validate their own requirements the same way (Azure OpenAI requires
`Endpoint` and `Deployment`; Ollama requires no key at all). If your app starts, your AI
configuration is structurally valid — remaining failures (wrong key value, unreachable
host) surface at call time as `AiException` with `Configuration`, `Authentication`, or
`Network` codes.

## Conditional registration

A common pattern (used by all the samples): register hosted providers only when their key is
present, keeping local development keyless:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));

    if (!string.IsNullOrEmpty(builder.Configuration["Koras:AI:OpenAI:ApiKey"]))
    {
        ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI")).AsDefault();
    }
});
```

Guard on the key before calling `AddOpenAI` — registering the provider without a key would
(correctly) fail startup validation.

## Options are fixed at startup

Options are read when the client is first built and the client is a cached singleton;
changing configuration afterwards does not affect a running process. Treat AI configuration
as immutable per process lifetime ([thread safety](../concepts/thread-safety.md)).

## Next steps

- [Configuration guide](../guides/configuration.md) — per-environment appsettings, multiple
  accounts of one provider, gateway endpoints.
- [Dependency injection](dependency-injection.md) — what to do with the clients you configured.
