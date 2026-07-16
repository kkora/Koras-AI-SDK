# Naming Guidelines

## Packages & namespaces

- PackageId == root namespace: `Koras.AI`, `Koras.AI.Abstractions`, `Koras.AI.{Provider}`.
- Provider names in IDs use official casing: `OpenAI`, `AzureOpenAI`, `Anthropic`, `Gemini`,
  `Ollama`.
- `IServiceCollection`/health-check/OTel-builder extensions live in the framework's own
  namespace (`Microsoft.Extensions.DependencyInjection`, `OpenTelemetry.Trace`, …);
  `KorasAiBuilder` extensions live in `Koras.AI`.

## Types

- Interfaces: capability nouns — `IChatClient`, `IEmbeddingClient`, `IChatClientFactory`,
  `IProviderHealthProbe`. No `Manager`, `Helper`, `Service` suffixes.
- The `Ai` compound is Pascal-cased as `Ai` inside identifiers (`AiTool`, `AiException`,
  `KorasAiBuilder`) but `AI` in package IDs / display names (brand form). Exception: none.
- Options classes end in `Options` and match the feature (`OpenAIOptions`, `RetryOptions`).
- Decorators end in `ChatClient` (`RetryChatClient`) and take `IChatClient innerClient` first.
- Extensible-enum pattern (`readonly record struct` + statics) for wire-driven vocabularies:
  `ChatRole`, `ChatFinishReason`, `ToolChoice`. True enums only for closed, library-owned sets
  (`AiErrorCode`, `ToolErrorBehavior`).

## Members

- Async methods end in `Async`; verbs per operation: `CompleteAsync` (chat), `StreamAsync`
  (streaming), `GenerateAsync` (embeddings), `ProbeAsync`, `RenderAsync`-style never blocks.
- `CancellationToken cancellationToken` is always the last parameter, always defaulted.
- Factory statics: `Create` (functional), `Declare` (declaration-only), `Parse` (throws) — no
  `TryParse` in the surface until user demand exists.
- No abbreviations except industry-standard: `Sse`, `Json`, `Http`, `Ai`, `Id`.
- Booleans read as assertions with safe default false: `EnableSensitiveData`, `CanInvoke`.

## Configuration keys

Bound sections mirror package structure: `"Koras:AI:OpenAI"`, `"Koras:AI:Anthropic"`. Option
property names bind 1:1 (`ApiKey`, `DefaultModel`, `Endpoint`).

## Telemetry names

Follow OTel semantic conventions: `gen_ai.*` tags; custom instruments under `koras.ai.*`
(dot-separated, lowercase). Activity source and meter: `"Koras.AI"`.
