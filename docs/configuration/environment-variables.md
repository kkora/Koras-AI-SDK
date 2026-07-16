# Configuration: Environment Variables

The standard .NET configuration system maps environment variables onto configuration keys —
a `:` separator in the key becomes a double underscore (`__`) in the variable name. Koras.AI
uses the `Koras:AI:<Provider>` section convention, so every option in
[the options reference](all-options.md) is settable from the environment. This is the
recommended way to deliver API keys.

## The mapping

| Configuration key | Environment variable |
|---|---|
| `Koras:AI:OpenAI:ApiKey` | `Koras__AI__OpenAI__ApiKey` |
| `Koras:AI:OpenAI:DefaultModel` | `Koras__AI__OpenAI__DefaultModel` |
| `Koras:AI:AzureOpenAI:Endpoint` | `Koras__AI__AzureOpenAI__Endpoint` |
| `Koras:AI:Anthropic:DefaultMaxOutputTokens` | `Koras__AI__Anthropic__DefaultMaxOutputTokens` |

Variable names are case-insensitive on Windows and matched case-insensitively by the
configuration provider elsewhere; the conventional casing above mirrors the section names.

## Per-provider examples

```bash
# OpenAI
export Koras__AI__OpenAI__ApiKey="sk-..."
export Koras__AI__OpenAI__DefaultModel="gpt-4o-mini"

# Azure OpenAI
export Koras__AI__AzureOpenAI__Endpoint="https://my-resource.openai.azure.com"
export Koras__AI__AzureOpenAI__ApiKey="..."
export Koras__AI__AzureOpenAI__Deployment="gpt-4o"

# Anthropic
export Koras__AI__Anthropic__ApiKey="sk-ant-..."
export Koras__AI__Anthropic__DefaultModel="claude-sonnet-4-5"

# Gemini
export Koras__AI__Gemini__ApiKey="..."
export Koras__AI__Gemini__DefaultModel="gemini-2.0-flash"

# Ollama (no key; endpoint only if not the default localhost:11434)
export Koras__AI__Ollama__Endpoint="http://ollama.internal:11434"
export Koras__AI__Ollama__DefaultModel="llama3.2"
```

The binding target is the section you pass at registration:

```csharp
builder.Services.AddKorasAI(ai =>
    ai.AddOpenAI(builder.Configuration.GetSection("Koras:AI:OpenAI")));
```

## Docker

```dockerfile
# Non-secret defaults can live in the image…
ENV Koras__AI__OpenAI__DefaultModel=gpt-4o-mini
```

```bash
# …keys are injected at run time, never baked in.
docker run -e Koras__AI__OpenAI__ApiKey="$OPENAI_KEY" myapp:latest
```

## Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
spec:
  template:
    spec:
      containers:
        - name: app
          image: myapp:latest
          env:
            - name: Koras__AI__OpenAI__DefaultModel
              value: gpt-4o-mini
            - name: Koras__AI__OpenAI__ApiKey
              valueFrom:
                secretKeyRef:
                  name: koras-ai-secrets
                  key: openai-api-key
```

```bash
kubectl create secret generic koras-ai-secrets \
  --from-literal=openai-api-key='sk-...'
```

## Precedence

With the default `WebApplication.CreateBuilder` / `Host.CreateApplicationBuilder` setup,
later providers override earlier ones:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User secrets (Development only)
4. **Environment variables**
5. Command-line arguments

So an environment variable overrides anything in `appsettings*.json`, and a command-line
switch (`--Koras:AI:OpenAI:DefaultModel=gpt-4o`) overrides the environment. This is what
makes the pattern work: commit non-secret defaults to `appsettings.json`, override models
per environment, and inject keys only at the deployment boundary.

## See also

- [appsettings.json layout](appsettings.md) — what belongs in files (and what must not).
- [Startup validation](validation.md) — how missing values fail at boot.
