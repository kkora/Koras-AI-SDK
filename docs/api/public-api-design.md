# Public API Design

The contract-first definition of the MVP surface. Types are C# 12/13, file-scoped namespaces,
nullable-enabled. Everything async takes a `CancellationToken` (last parameter, default
`default`). All clients and decorators are thread-safe for singleton use.

## Koras.AI.Abstractions

### Clients

```csharp
namespace Koras.AI;

public interface IChatClient
{
    /// Provider identifier, e.g. "openai", "anthropic". Stable, lowercase.
    string ProviderName { get; }

    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// Streaming variant. Transport opens on first MoveNextAsync; disposing the enumerator
    /// releases the connection. Throws AiException for pre-stream and mid-stream failures.
    IAsyncEnumerable<ChatStreamUpdate> StreamAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

public interface IEmbeddingClient
{
    string ProviderName { get; }
    Task<EmbeddingResponse> GenerateAsync(EmbeddingRequest request, CancellationToken cancellationToken = default);
}

/// Optional capability: providers expose a cheap liveness probe (used by health checks).
public interface IProviderHealthProbe
{
    Task ProbeAsync(CancellationToken cancellationToken = default);
}
```

### Messages and requests

```csharp
public sealed class ChatMessage           // mutable-free after construction
{
    public ChatRole Role { get; }
    public string? Text { get; }
    public IReadOnlyList<ToolCall> ToolCalls { get; }          // assistant messages
    public string? ToolCallId { get; }                          // tool-result messages

    public static ChatMessage System(string text);
    public static ChatMessage User(string text);
    public static ChatMessage Assistant(string text);
    public static ChatMessage Assistant(string? text, IReadOnlyList<ToolCall> toolCalls);
    public static ChatMessage ToolResult(string toolCallId, string content);
}

public readonly record struct ChatRole(string Value)  // extensible enum pattern
{
    public static ChatRole System { get; }
    public static ChatRole User { get; }
    public static ChatRole Assistant { get; }
    public static ChatRole Tool { get; }
}

public sealed class ChatRequest
{
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public string? Model { get; init; }              // null → client's default model
    public ChatOptions? Options { get; init; }

    public static ChatRequest FromPrompt(string prompt, string? systemPrompt = null);
}

public sealed class ChatOptions
{
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
    public IReadOnlyList<string>? StopSequences { get; init; }
    public ChatResponseFormat? ResponseFormat { get; init; }
    public IReadOnlyList<AiTool>? Tools { get; init; }
    public ToolChoice? ToolChoice { get; init; }
    /// Provider-specific request fields merged into the wire request (escape hatch).
    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }
}
```

### Responses

```csharp
public sealed class ChatResponse
{
    public required ChatMessage Message { get; init; }
    public required string Provider { get; init; }
    public string? Model { get; init; }
    public ChatFinishReason FinishReason { get; init; }
    public TokenUsage Usage { get; init; }
    public string? ResponseId { get; init; }
    /// Raw provider payload for fields the model doesn't surface. May be default.
    public JsonElement RawRepresentation { get; init; }

    public string? Text { get; }                      // convenience → Message.Text
}

public readonly record struct ChatFinishReason(string Value)
{
    public static ChatFinishReason Stop { get; }
    public static ChatFinishReason Length { get; }
    public static ChatFinishReason ToolCalls { get; }
    public static ChatFinishReason ContentFilter { get; }
    public static ChatFinishReason Unknown { get; }
}

public readonly record struct TokenUsage(int InputTokens, int OutputTokens)
{
    public int TotalTokens { get; }
    public static TokenUsage operator +(TokenUsage a, TokenUsage b);
}

public sealed class ChatStreamUpdate
{
    public string? TextDelta { get; init; }
    public ToolCallDelta? ToolCallDelta { get; init; }
    public ChatFinishReason? FinishReason { get; init; }   // set on terminal update
    public TokenUsage? Usage { get; init; }                // set when provider reports it
    public string? ResponseId { get; init; }
}
```

### Tools

```csharp
public class AiTool
{
    public string Name { get; }
    public string? Description { get; }
    public JsonElement ParametersSchema { get; }           // JSON Schema object

    public static AiTool Create(string name, string? description, Delegate handler);      // schema from parameters
    public static AiTool Declare(string name, string? description, JsonElement parametersSchema); // declaration-only
    public bool CanInvoke { get; }
    public Task<string> InvokeAsync(string argumentsJson, CancellationToken ct = default); // throws for declaration-only
}

public sealed class ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ArgumentsJson { get; init; }
    public T? ParseArguments<T>();                          // STJ, throws AiException(InvalidResponse)
}

public sealed class ToolCallDelta { public string? Id; public string? Name; public string? ArgumentsJsonDelta; public int Index; }

public readonly record struct ToolChoice(string Value)
{
    public static ToolChoice Auto { get; }
    public static ToolChoice None { get; }
    public static ToolChoice Required { get; }
    public static ToolChoice Tool(string name);
}
```

### Response formats & structured output

```csharp
public abstract class ChatResponseFormat
{
    public static ChatResponseFormat Text { get; }
    public static ChatResponseFormat Json { get; }                       // "JSON mode"
    public static ChatResponseFormat JsonSchema(string name, JsonElement schema, bool strict = true);
    public static ChatResponseFormat ForType<T>();                       // schema generated from T
}
```

### Embeddings

```csharp
public sealed class EmbeddingRequest
{
    public required IReadOnlyList<string> Values { get; init; }
    public string? Model { get; init; }
    public int? Dimensions { get; init; }
    public EmbeddingRequest();                                          // for object-init
    public EmbeddingRequest(params string[] values);
}

public sealed class Embedding { public ReadOnlyMemory<float> Vector { get; } public int Index { get; } }

public sealed class EmbeddingResponse
{
    public required IReadOnlyList<Embedding> Embeddings { get; init; }
    public required string Provider { get; init; }
    public string? Model { get; init; }
    public TokenUsage Usage { get; init; }
}
```

### Errors

See [error model](../architecture/error-model.md) — `AiException`, `AiErrorCode`.

## Koras.AI (core)

### DI (namespace `Microsoft.Extensions.DependencyInjection` for the entry point)

```csharp
public static class KorasAiServiceCollectionExtensions
{
    public static IServiceCollection AddKorasAI(this IServiceCollection services, Action<KorasAiBuilder> configure);
}
```

```csharp
namespace Koras.AI;

public sealed class KorasAiBuilder
{
    public IServiceCollection Services { get; }

    // Generic registration — used by provider packages and custom providers.
    public KorasAiClientBuilder AddClient(string name, Func<IServiceProvider, IChatClient> factory);
    public KorasAiClientBuilder AddEmbeddingClient(string name, Func<IServiceProvider, IEmbeddingClient> factory);
    public KorasAiClientBuilder AddFallback(string name, params string[] clientNames);

    // Global decorators (applied to every chat client, outermost-last registration order).
    public KorasAiBuilder Use(Func<IServiceProvider, IChatClient, IChatClient> decorator);
    public KorasAiBuilder UseRetry(Action<RetryOptions>? configure = null);
    public KorasAiBuilder UseToolInvocation(Action<ToolInvocationOptions>? configure = null);
    public KorasAiBuilder ConfigureTelemetry(Action<KorasAiTelemetryOptions> configure);
}

public sealed class KorasAiClientBuilder   // returned per named client
{
    public string Name { get; }
    public KorasAiClientBuilder AsDefault();
    public KorasAiClientBuilder Use(Func<IServiceProvider, IChatClient, IChatClient> decorator);
}

public interface IChatClientFactory
{
    IChatClient GetChatClient(string name);
    IEmbeddingClient GetEmbeddingClient(string name);
    IReadOnlyList<string> ClientNames { get; }
}
```

`AddKorasAI` also registers: default `IChatClient` / `IEmbeddingClient` (singleton, resolved
through the factory), `IChatClientFactory` (singleton), options with `ValidateOnStart`.

### Decorators & resilience

```csharp
public abstract class DelegatingChatClient(IChatClient innerClient) : IChatClient
{
    protected IChatClient InnerClient { get; }
    public virtual string ProviderName { get; }
    public virtual Task<ChatResponse> CompleteAsync(...);
    public virtual IAsyncEnumerable<ChatStreamUpdate> StreamAsync(...);
}

public sealed class RetryOptions
{
    public int MaxAttempts { get; set; } = 3;                       // total attempts incl. first
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(100);
    public bool HonorRetryAfter { get; set; } = true;
}

public sealed class ToolInvocationOptions
{
    public int MaxIterations { get; set; } = 8;
    public ToolErrorBehavior ErrorBehavior { get; set; } = ToolErrorBehavior.ReturnToModel;
}
public enum ToolErrorBehavior { ReturnToModel, Throw }
```

### Structured output & convenience extensions

```csharp
public static class ChatClientExtensions
{
    public static Task<ChatResponse> CompleteAsync(this IChatClient client, string prompt, CancellationToken ct = default);
    public static Task<ChatResponse<T>> CompleteAsync<T>(this IChatClient client, ChatRequest request, JsonSerializerOptions? serializerOptions = null, CancellationToken ct = default);
    public static Task<ChatResponse<T>> CompleteAsync<T>(this IChatClient client, string prompt, CancellationToken ct = default);
}

public sealed class ChatResponse<T>
{
    public required T Value { get; init; }
    public required ChatResponse Raw { get; init; }
}
```

### Prompt templates (`Koras.AI.Templates`)

```csharp
public sealed class PromptTemplate
{
    public static PromptTemplate Parse(string template);            // throws FormatException with position
    public IReadOnlyList<string> ParameterNames { get; }
    public string Render(IReadOnlyDictionary<string, object?> values); // missing key → KeyNotFoundException naming the placeholder
    public string Render(object values);                             // property-bag overload
}
```

### Provider plumbing (`Koras.AI.Providers`) — for provider authors

```csharp
public abstract class ProviderChatClient : IChatClient      // HTTP send, error normalization, JSON parse
public abstract class ProviderEmbeddingClient : IEmbeddingClient
public static class SseReader     // IAsyncEnumerable<SseEvent> ReadAsync(Stream, CancellationToken)
public static class JsonLinesReader
public static class ProviderErrors // AiException factory helpers (FromHttpResponse, Network, Timeout)
```

## Provider packages (shape shown for OpenAI; others mirror it)

```csharp
namespace Koras.AI;   // builder extensions live in Koras.AI for discoverability

public static class OpenAIKorasAiBuilderExtensions
{
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, Action<OpenAIOptions> configure);
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, string name, Action<OpenAIOptions> configure);
    public static KorasAiClientBuilder AddOpenAI(this KorasAiBuilder ai, IConfiguration configuration);   // binds + validates
}

namespace Koras.AI.OpenAI;

public sealed class OpenAIOptions
{
    public string? ApiKey { get; set; }                       // required (validated)
    public Uri Endpoint { get; set; } = new("https://api.openai.com/v1/");
    public string? DefaultModel { get; set; }                 // required
    public string? DefaultEmbeddingModel { get; set; }
    public string? Organization { get; set; }
}
```

Azure: `AddAzureOpenAI` with `Endpoint` (resource URL), `Deployment`, `ApiKey`, `ApiVersion`.
Anthropic: `ApiKey`, `DefaultModel`, `MaxTokensDefault` (Anthropic requires max_tokens),
`AnthropicVersion`. Gemini: `ApiKey`, `DefaultModel`, `Endpoint`. Ollama: `Endpoint`
(default `http://localhost:11434`), `DefaultModel`.

## Integration packages

```csharp
// Koras.AI.AspNetCore  (namespace Microsoft.Extensions.DependencyInjection)
public static class KorasAiHealthChecksBuilderExtensions
{
    public static IHealthChecksBuilder AddKorasAI(this IHealthChecksBuilder builder,
        string? clientName = null, string? healthCheckName = null,
        HealthStatus failureStatus = HealthStatus.Unhealthy, IEnumerable<string>? tags = null);
}

// Koras.AI.OpenTelemetry  (namespace OpenTelemetry.Trace / OpenTelemetry.Metrics)
public static TracerProviderBuilder AddKorasAI(this TracerProviderBuilder builder);
public static MeterProviderBuilder AddKorasAI(this MeterProviderBuilder builder);
```

## Design-rule compliance

- No static mutable state (statics are readonly singletons: `ChatRole.User`, the shared
  `ActivitySource`/`Meter`).
- No boolean parameter overloads; behavior variation via options types.
- No third-party types in any public signature (BCL + Microsoft.Extensions.* + OTel builder
  types in the OTel package only — the package's purpose).
- Hidden network calls: none — every network operation is an explicit `*Async` on a client.
- Generic complexity: single-`T` generics only (`CompleteAsync<T>`, `ForType<T>`).
