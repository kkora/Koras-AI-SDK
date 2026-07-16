# Concepts Overview

Koras.AI is a provider-neutral .NET SDK for AI models. This page gives you the mental model;
the rest of the concepts section drills into each part.

## The four layers

```
Application code
      │  depends on interfaces only
      ▼
Koras.AI.Abstractions      contracts: IChatClient, IEmbeddingClient, models, AiException
Koras.AI (core)            composition: DI builder, retry, fallback, tool loop,
                           structured output, prompt templates, logging/telemetry
Provider packages          Koras.AI.OpenAI / .AzureOpenAI / .Anthropic / .Gemini / .Ollama
Integration packages       Koras.AI.AspNetCore (health checks), Koras.AI.OpenTelemetry
```

- **Abstractions** is where your code lives conceptually. If you write a library, reference
  only this package.
- **Core** owns everything that is true regardless of provider: how clients compose, retry,
  fail over, log, and trace.
- **Providers** are thin adapters from the contracts to each vendor's REST API.
- **Integrations** connect the SDK to frameworks without bloating the core.

See [architecture](architecture.md) for the pipeline details and links to the full
architecture documentation.

## Provider-neutral vs. provider-specific

The abstraction models what is common across providers:

| Provider-neutral (in the contracts) | Provider-specific (configured per package) |
|---|---|
| Messages, roles, requests, options | API keys, endpoints, API versions |
| Tools and tool calls | Default models and deployments |
| Response format (text / JSON / JSON Schema) | Anthropic's mandatory `max_tokens` default |
| Token usage, finish reasons | Probe endpoints for health checks |
| The `AiException`/`AiErrorCode` error taxonomy | Wire-error → error-code mapping |

Your request-building, response-handling, and error-handling code is identical for every
provider. Switching providers is a DI registration change
([dependency injection](../getting-started/dependency-injection.md)).

## Escape hatches

Provider neutrality must not lock you out of provider capabilities. Two escape hatches keep
the abstraction honest:

**`ChatOptions.AdditionalProperties`** — extra request fields merged into the wire request
verbatim. Use it for provider features the model does not surface:

```csharp
var request = new ChatRequest
{
    Messages = [ChatMessage.User("Explain quantum entanglement.")],
    Options = new ChatOptions
    {
        AdditionalProperties = new Dictionary<string, object?>
        {
            ["seed"] = 42,                    // an OpenAI-specific request field
        },
    },
};
```

**`ChatResponse.RawRepresentation`** — the provider's raw JSON payload as a `JsonElement`,
for response fields the model does not map:

```csharp
ChatResponse response = await chat.CompleteAsync(request);
if (response.RawRepresentation.ValueKind == JsonValueKind.Object
    && response.RawRepresentation.TryGetProperty("system_fingerprint", out JsonElement fp))
{
    Console.WriteLine($"Fingerprint: {fp}");
}
```

Code using the escape hatches is deliberately provider-specific — keep it isolated so the
rest of your application stays portable.

## The key contracts in one glance

```csharp
public interface IChatClient
{
    string ProviderName { get; }
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
```

Everything else hangs off this interface: decorators wrap it, providers implement it, your
tests fake it. [Core abstractions](core-abstractions.md) walks through every type.

## Guarantees you can rely on

- **Singleton-safe:** every client and decorator is thread-safe
  ([thread safety](thread-safety.md)).
- **One error type:** failures are `AiException` with a closed `AiErrorCode` taxonomy;
  cancellation is `OperationCanceledException`
  ([error handling](error-handling.md), [cancellation](cancellation.md)).
- **No hidden network calls:** every network operation is an explicit `*Async` call
  ([lifecycle](lifecycle.md)).
- **Content is private by default:** prompts and responses never appear in logs or traces
  unless you enable `EnableSensitiveData`.
