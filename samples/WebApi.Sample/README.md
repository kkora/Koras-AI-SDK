# Web API Sample

Controllers + **provider fallback** + **health checks**. The default client is a fallback chain
(`openai → anthropic → ollama`) built from whichever providers have credentials configured;
local Ollama is always the last resort.

## Setup

```bash
# optional hosted providers (user secrets — never appsettings.json):
dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."
dotnet user-secrets set "Koras:AI:Anthropic:ApiKey" "sk-ant-..."
# local fallback:
ollama pull llama3.2
```

## Run

```bash
dotnet run
curl -s localhost:5000/api/chat -H 'content-type: application/json' -d '{"prompt":"hello"}'
curl -s localhost:5000/api/chat/clients        # configured client names
curl -s localhost:5000/health                  # probes Ollama via /api/version
```

## What to look at

- `Program.cs` — conditional provider registration and `AddFallback(...).AsDefault()`.
- `ChatController` — mapping `AiException` codes to HTTP semantics (429 with retry-after,
  422 for content filter, 503 for transient outages).
- Logs — `Koras.AI` category shows completions, retries, and failovers.

## Error scenarios

Stop Ollama and use a bad OpenAI key → terminal `Authentication` errors do **not** fail over
(by design); transient errors do. `/health` reports `Unhealthy` when Ollama is unreachable.

Docs: [Provider fallback](../../docs/features/provider-fallback.md),
[Health checks](../../docs/features/health-checks.md),
[ASP.NET Core guide](../../docs/guides/aspnet-core.md).
