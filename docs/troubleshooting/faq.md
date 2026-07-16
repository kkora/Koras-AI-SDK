# FAQ

## Which package do I install?

One package per provider you use — it pulls in everything else transitively:
`Koras.AI.OpenAI`, `Koras.AI.AzureOpenAI`, `Koras.AI.Anthropic`, `Koras.AI.Gemini`, or
`Koras.AI.Ollama`. Add `Koras.AI.AspNetCore` for health checks and `Koras.AI.OpenTelemetry`
for OTel wiring. Libraries that only *consume* `IChatClient` should reference just
`Koras.AI.Abstractions`.

## Can I use two providers at the same time?

Yes — that's a core scenario. Register both inside `AddKorasAI` and resolve by name via
`IChatClientFactory`, or compose them with `AddFallback`. See
[advanced scenarios](../recipes/advanced-scenarios.md).

## How do I switch providers?

If you registered from configuration, change the registration call
(`AddOpenAI` → `AddAnthropic`) and the config section — application code against
`IChatClient` doesn't change. Requests, responses, errors, and telemetry are
provider-neutral by design; only `AdditionalProperties` and `RawRepresentation` usage is
provider-coupled.

## Does it support .NET Framework?

No. The SDK targets `net8.0`, `net9.0`, and `net10.0` — no `netstandard2.0` and no .NET
Framework (see [ADR-0002](../architecture/decision-records/adr-0002-target-frameworks.md)).
A TFM is dropped only in the next major release after Microsoft support for it ends.

## How are tokens counted?

The SDK reports what the provider returns: `ChatResponse.Usage` is a
`TokenUsage(InputTokens, OutputTokens)` straight from the provider's response (streamed
responses carry usage on the final update when the provider emits it). The SDK does no
client-side tokenization — counts are the provider's own, which is what you're billed on.

## Is my data logged?

Not by default. Prompts and completions never appear in logs, exceptions, or traces unless
you opt in with `EnableSensitiveData = true` *and* `Trace`-level logging — a local-debugging
switch. API keys are never logged under any setting. See [logging](logging.md).

## How do I use an OpenAI-compatible gateway (LiteLLM, OpenRouter, vLLM, …)?

Point `OpenAIOptions.Endpoint` at the gateway:

```csharp
ai.AddOpenAI(o =>
{
    o.Endpoint = new Uri("https://gateway.internal.example/v1/");
    o.ApiKey = gatewayKey;
    o.DefaultModel = "my-routed-model";
});
```

The endpoint must be HTTPS unless it's a loopback address.

## Why can't I get embeddings from Anthropic?

Anthropic doesn't offer an embeddings API, so `AddAnthropic` registers only a chat client
and embedding calls routed there fail with `AiErrorCode.NotSupported`. Use OpenAI, Azure
OpenAI, Gemini, or Ollama for embeddings — mixing providers per capability is normal.

## How do I mock the client in tests?

Implement `IChatClient` directly — it's two methods plus a name; no mocking library needed.
Queue scripted responses and assert on captured `ChatRequest`s. Full patterns, including
in-process wire-level fakes, in [testing recipes](../recipes/testing-recipes.md).

## When will 1.0 ship?

The current version is `0.1.0-preview.1`. Per the
[versioning policy](../migration/versioning-policy.md), 0.x minors may still break with
migration notes; 0.5.0 is the API-freeze candidate, and from 1.0 interfaces are frozen with
full backward-compatibility guarantees. Dates live on the [roadmap](../../ROADMAP.md).

## How does this relate to Microsoft.Extensions.AI?

They occupy the same layer (provider-neutral AI abstractions for .NET). Koras.AI competes on
the productized whole: uniform first-party providers over raw REST, a normalized error
taxonomy with `IsTransient`, built-in retry/fallback, startup-validated options, and
GenAI-convention telemetry out of the box. A two-way M.E.AI bridge (F-022) is planned for
1.1 so adoption is reversible — see the [future roadmap](../features/future-roadmap.md).

## Does it support images or other multimodal content?

Not yet. MVP is text-first; multimodal content parts (images) are planned for 1.1 (F-020) —
the message model was shaped with that extension in mind. Until then, provider-specific
options via `ChatOptions.AdditionalProperties` are not a supported path for multimodal
payloads.

## Can I add a provider you don't ship?

Yes — implement `IChatClient` (the `Koras.AI.Providers` base classes give you HTTP plumbing,
SSE/JSON-lines readers, and error normalization helpers) and register it with
`ai.AddClient("mine", sp => ...)`. Retry, fallback, telemetry, and health checks work
unchanged. See [custom providers](../features/custom-providers.md).

## Why does startup fail instead of the first request?

Deliberate: options are validated with `ValidateOnStart`, so a missing key or an HTTP
endpoint fails deployment loudly instead of failing user traffic later. See
[validation](../configuration/validation.md).
