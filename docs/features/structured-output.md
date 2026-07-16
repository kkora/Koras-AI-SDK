# Structured Output

## Overview

Structured output makes the model return JSON that conforms to a schema and hands you a
deserialized .NET object. The generic extension `client.CompleteAsync<T>(...)` does the whole
round trip: it applies `ChatResponseFormat.ForType<T>()` (a JSON Schema generated from `T`)
when the request has no explicit `ResponseFormat`, calls the model, strips a Markdown code
fence if a lenient provider added one, deserializes, and returns
`ChatResponse<T> { Value, Raw }`.

Provider mechanics differ but are hidden: OpenAI and Azure OpenAI use `json_schema`, Gemini
uses `responseSchema`, Ollama uses its `format` parameter, and Anthropic uses a forced
synthetic tool named `record_output` (see [provider-anthropic.md](provider-anthropic.md)).

## When to use it

Use it whenever the answer feeds code rather than a human: extraction, classification,
routing decisions, form filling.

## Required packages

- `Koras.AI` plus a provider package.

## Basic usage

```csharp
using Koras.AI;

public sealed record Invoice(string Number, decimal Total, DateOnly DueDate);

ChatResponse<Invoice> result = await client.CompleteAsync<Invoice>(
    $"Extract the invoice fields from this email:\n{emailBody}");

Invoice invoice = result.Value;
Console.WriteLine($"{invoice.Number}: {invoice.Total} due {invoice.DueDate}");
Console.WriteLine($"tokens: {result.Raw.Usage.TotalTokens}");
```

The full-request overload lets you keep conversation history and options; pass custom
`JsonSerializerOptions` when your contract needs them:

```csharp
var request = new ChatRequest
{
    Messages = [ChatMessage.System("Extract data precisely."), ChatMessage.User(emailBody)],
    Options = new ChatOptions { Temperature = 0 },
};

ChatResponse<Invoice> result = await client.CompleteAsync<Invoice>(request, serializerOptions: null, ct);
```

## Advanced configuration

You can set the response format manually instead of relying on the automatic application:

- `ChatResponseFormat.Json` — free-form "JSON mode" without a schema.
- `ChatResponseFormat.JsonSchema(name, schema, strict: true)` — bring your own `JsonElement` schema.
- `ChatResponseFormat.ForType<T>()` — the schema `CompleteAsync<T>` generates.

```csharp
Options = new ChatOptions { ResponseFormat = ChatResponseFormat.ForType<Invoice>() }
```

When the request already carries a `ResponseFormat`, `CompleteAsync<T>` respects it and only
performs the deserialization step. Annotate contract members with
`System.ComponentModel.DescriptionAttribute` to guide the model.

## Dependency-injection usage

`CompleteAsync<T>` is an extension over any `IChatClient`, so it works with the injected
default client and with named clients from `IChatClientFactory`. See
[dependency injection](dependency-injection.md).

## Error handling

If the model's output cannot be deserialized into `T` (or deserializes to `null`), the
extension throws `AiException` with `AiErrorCode.InvalidResponse`; the offending text (up to
4 KB) is attached as `ProviderErrorBody` and the provider name as `Provider`.

```csharp
try
{
    ChatResponse<Invoice> result = await client.CompleteAsync<Invoice>(prompt, ct);
}
catch (AiException ex) when (ex.Code == AiErrorCode.InvalidResponse)
{
    logger.LogWarning(ex, "Model returned unparsable output: {Body}", ex.ProviderErrorBody);
}
```

All transport failures surface exactly as for plain [chat completion](chat-completion.md).

## Cancellation

Pass the `CancellationToken` as the last argument; cancellation surfaces as
`OperationCanceledException`.

## Provider notes

| Provider | Mechanism | Notes |
|---|---|---|
| OpenAI / Azure OpenAI | `json_schema` response format | `strict` honored |
| Anthropic | forced `record_output` tool | prefer `CompleteAsync<T>` over streaming |
| Gemini | `responseSchema` | unsupported schema keywords (`additionalProperties`, `$schema`, `$defs`) are stripped |
| Ollama | `format` parameter | model-dependent fidelity |

## Security considerations

Structured output is still model output — validate business rules (ranges, referential
checks) before acting on `Value`. Never echo `ProviderErrorBody` to end users; it may contain
prompt content.

## Thread safety

`ChatResponseFormat` instances and generated schemas are immutable; the extension methods are
stateless. Safe for concurrent use on singleton clients.

## Testing applications using this feature

A fake `IChatClient` that returns JSON text exercises the deserialization path for real:

```csharp
private sealed class JsonChatClient(string json) : IChatClient
{
    public string ProviderName => "fake";

    public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse
        {
            Message = ChatMessage.Assistant(json),
            Provider = ProviderName,
            FinishReason = ChatFinishReason.Stop,
        });

    public IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

// var client = new JsonChatClient("""{"number":"INV-1","total":5,"dueDate":"2026-08-01"}""");
```

## Common mistakes

- Streaming a schema-constrained request on Anthropic — the payload arrives as tool-call
  deltas, not text; use `CompleteAsync<T>`.
- Using deeply recursive or `additionalProperties`-dependent schemas on Gemini, whose schema
  dialect is narrower (the SDK strips the unsupported keywords).
- Treating deserialization success as validation — a well-formed `Invoice` can still contain
  hallucinated values.
- Setting `ChatResponseFormat.Json` and expecting a specific shape; JSON mode has no schema.

## Related features

- [Chat completion](chat-completion.md)
- [Tool calling](tool-calling.md)
- [Error handling](error-handling.md)
- [Provider guides](provider-openai.md)
