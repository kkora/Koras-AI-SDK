# Architecture Overview

## Layering

```
┌───────────────────────────────────────────────────────────────┐
│  Application (ASP.NET Core, Worker, Console, Blazor)          │
├───────────────────────────────────────────────────────────────┤
│  Koras.AI.AspNetCore   Koras.AI.OpenTelemetry   (integrations)│
├───────────────────────────────────────────────────────────────┤
│  Koras.AI  (core: DI builder, pipeline/decorators, retry,     │
│             fallback, structured output, tool loop, templates,│
│             telemetry, provider base classes)                 │
├──────────┬──────────────┬───────────┬──────────┬──────────────┤
│ .OpenAI  │ .AzureOpenAI │ .Anthropic│ .Gemini  │ .Ollama      │
├──────────┴──────────────┴───────────┴──────────┴──────────────┤
│  Koras.AI.Abstractions (contracts + models + errors)          │
└───────────────────────────────────────────────────────────────┘
```

- **Applications and libraries** code against `Koras.AI.Abstractions` types.
- **Provider packages** implement the contracts over each provider's REST API, using shared
  plumbing from `Koras.AI` (`Koras.AI.Providers` namespace).
- **Core** owns everything provider-neutral: composition, resilience, observability.
- **Integration packages** stay tiny and depend on their framework (ASP.NET Core, OTel).

## Package inventory

| Package | Depends on | Contents |
|---|---|---|
| `Koras.AI.Abstractions` | System.Text.Json (net8 only, for schema types) | `IChatClient`, `IEmbeddingClient`, models, `AiException`, `AiErrorCode`, `AiTool`, `ChatStreamUpdate` |
| `Koras.AI` | Abstractions, M.E.Options(+Config binder), M.E.Logging.Abstractions, M.E.DependencyInjection.Abstractions, M.E.Http | `KorasAiBuilder`, `AddKorasAI`, decorators (retry, fallback, tool loop, telemetry, logging), `PromptTemplate`, structured output, `IChatClientFactory`, `Koras.AI.Providers.*` base classes |
| `Koras.AI.OpenAI` | Koras.AI | `OpenAIChatClient`, `OpenAIEmbeddingClient`, `OpenAIOptions`, `AddOpenAI` |
| `Koras.AI.AzureOpenAI` | Koras.AI.OpenAI | Azure endpoint/auth variant reusing the OpenAI wire protocol |
| `Koras.AI.Anthropic` | Koras.AI | Messages API adapter |
| `Koras.AI.Gemini` | Koras.AI | generateContent adapter |
| `Koras.AI.Ollama` | Koras.AI | native `/api/chat`, `/api/embed` adapter |
| `Koras.AI.AspNetCore` | Koras.AI, M.E.Diagnostics.HealthChecks.Abstractions | health checks |
| `Koras.AI.OpenTelemetry` | Koras.AI, OpenTelemetry.Api | `WithKorasAI()` registration sugar |

## Key runtime flow (chat completion)

1. Consumer resolves `IChatClient` (default) or `IChatClientFactory.GetChatClient("name")`.
2. The resolved instance is a decorator chain built by the DI builder:
   `Telemetry → Logging → [ToolInvocation] → Retry → [Fallback →] ProviderClient`.
3. The provider client maps `ChatRequest` → provider JSON, sends via a named `HttpClient`
   from `IHttpClientFactory`, maps the response (or error) back to `ChatResponse`/`AiException`.
4. Streaming follows the same chain; decorators that cannot re-enter a live stream (retry,
   fallback) only act before the first emitted update.

## Design tenets

- **Dependency direction is inward:** everything depends on Abstractions; Abstractions depends
  on nothing (beyond the BCL). Providers never reference each other (Azure→OpenAI is the single
  sanctioned exception, ADR-0003).
- **Composition over configuration flags:** cross-cutting behavior is a decorator, not a
  boolean on the client.
- **No statics, no service locator:** all state flows through constructors; `TimeProvider`
  injects time; ambient context limited to `Activity.Current` (standard).
- **Thread-safety:** every public client and decorator is safe for concurrent singleton use.

See [package-boundaries](package-boundaries.md), [dependency-rules](dependency-rules.md),
[extension-model](extension-model.md), [error-model](error-model.md),
[observability](observability.md), [diagrams](diagrams.md), and the
[decision records](decision-records/).
