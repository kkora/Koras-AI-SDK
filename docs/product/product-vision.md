# Koras.AI — Product Vision

## Vision statement

**Koras.AI is the one AI client a .NET application needs.** One set of strongly typed abstractions
for chat, streaming, structured output, tool calling, and embeddings — with interchangeable
providers (OpenAI, Azure OpenAI, Anthropic, Google Gemini, Ollama) and the enterprise plumbing
that production systems actually require: dependency injection, options validation, retries,
provider fallback, normalized errors, logging, metrics, and distributed tracing.

## Why this package should exist

Every .NET team that integrates more than one model provider — or wants the *option* to switch
providers — rebuilds the same infrastructure:

- an HTTP client per provider with its own request/response models,
- SSE stream parsing, three different ways,
- retry loops that may or may not honor `Retry-After`,
- ad-hoc error handling where a 429 from OpenAI and a 529 from Anthropic look nothing alike,
- token-usage tracking bolted on with logging statements.

Koras.AI turns that recurring cost into a package reference.

## Product principles

1. **Provider-neutral by contract.** Application code depends only on `Koras.AI.Abstractions`.
   Swapping OpenAI for Anthropic is a configuration change, not a refactor.
2. **Secure by default.** No secrets in code or logs, header-based authentication, startup
   validation of configuration, bounded retries.
3. **Light by design.** Providers are implemented over raw REST. No vendor SDK dependency ever
   appears in the dependency graph or leaks into the public API.
4. **Observable by default.** `ILogger`, `ActivitySource`, and `Meter` instrumentation following
   the OpenTelemetry GenAI semantic conventions ships in the core, with zero OpenTelemetry
   package dependency required.
5. **Boring where it should be.** DI registration, the options pattern, `CancellationToken`,
   `IAsyncEnumerable` streaming — idiomatic modern .NET, nothing invented.

## What Koras.AI is *not*

- Not an agent framework or orchestration engine (Semantic Kernel's job). Agent orchestration is
  at most a future add-on package.
- Not a prompt-engineering IDE, evaluation harness, or vector database.
- Not a wrapper over official vendor SDKs.
- Not a kitchen sink: RAG, memory, and semantic caching are deliberate roadmap items, shipped as
  separate packages only when the core has proven stable.

## Success criteria

- A developer goes from `dotnet add package` to a streaming chat response in under five minutes.
- Switching provider requires changing only configuration and the provider package reference.
- An unhandled provider outage degrades gracefully through configured fallback.
- Operations teams get token usage and latency in their existing OTel pipeline with one line.

## North-star metric

Monthly NuGet downloads of `Koras.AI.Abstractions` (the package every consumer shares), with a
secondary metric of the number of distinct provider packages installed together — evidence the
provider-neutral promise is being exercised.
