# Advanced Dependency Injection

Beyond the basics ([getting started](../getting-started/dependency-injection.md)): shaping
individual clients, writing your own decorators, and overriding services for tests.

## Per-client decorators

`KorasAiBuilder.Use*` applies to every chat client. To decorate just one, use the
`KorasAiClientBuilder` returned by the registration:

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI("openai", o => { o.ApiKey = key; o.DefaultModel = "gpt-4o-mini"; })
      .Use((sp, inner) => new BudgetGuardChatClient(inner, maxTokensPerCall: 2_000));

    ai.AddOllama(o => o.DefaultModel = "llama3.2");   // not budget-guarded

    ai.UseRetry();                                    // global — wraps both
});
```

Ordering: per-client decorators wrap the provider client first, then global decorators wrap
the result, then the SDK adds logging and telemetry outermost
([architecture](../concepts/architecture.md)). So the pipeline for `"openai"` above is
`Telemetry → Logging → Retry → BudgetGuard → OpenAI`.

## Writing a custom decorator

Derive from `DelegatingChatClient`, override what you need, delegate the rest. The base
class mirrors `DelegatingHandler` and forwards `CompleteAsync`, `StreamAsync`, and
`ProviderName` to the inner client by default:

```csharp
public sealed class BudgetGuardChatClient(IChatClient innerClient, int maxTokensPerCall)
    : DelegatingChatClient(innerClient)
{
    public override Task<ChatResponse> CompleteAsync(
        ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Options?.MaxOutputTokens is null or > 0 &&
            (request.Options?.MaxOutputTokens ?? int.MaxValue) > maxTokensPerCall)
        {
            request = new ChatRequest
            {
                Messages = request.Messages,
                Model = request.Model,
                Options = new ChatOptions
                {
                    Temperature = request.Options?.Temperature,
                    TopP = request.Options?.TopP,
                    MaxOutputTokens = maxTokensPerCall,
                    StopSequences = request.Options?.StopSequences,
                    ResponseFormat = request.Options?.ResponseFormat,
                    Tools = request.Options?.Tools,
                    ToolChoice = request.Options?.ToolChoice,
                    AdditionalProperties = request.Options?.AdditionalProperties,
                },
            };
        }

        return base.CompleteAsync(request, cancellationToken);
    }
}
```

Rules for decorators:

- **Thread-safe, stateless where possible** — one instance serves all concurrent calls
  ([thread safety](../concepts/thread-safety.md)).
- **Requests are immutable** — to modify one, build a new `ChatRequest` (as above), never
  mutate.
- **Preserve the error contract** — throw `AiException` for failures, let
  `OperationCanceledException` pass through.
- Register globally with `ai.Use((sp, inner) => new BudgetGuardChatClient(inner, 2_000))`
  or per client as shown earlier. The `IServiceProvider` parameter lets you resolve
  dependencies (loggers, options) for the decorator.

`DelegatingChatClient.GetService(Type)` walks the chain to find capabilities (the health
check uses it to locate `IProviderHealthProbe` through any decorator stack) — override it
only if your decorator deliberately hides the inner client.

## Resolving TimeProvider

The built-in retry decorator takes its time source from DI: register a `TimeProvider` and
backoff delays flow through it — which is how the SDK's own tests fast-forward retries:

```csharp
// Production: nothing to do; TimeProvider.System is the default.
// Tests:
services.AddSingleton<TimeProvider>(new FakeTimeProvider());
```

Custom decorators that need time should follow the same pattern:
`sp.GetService<TimeProvider>() ?? TimeProvider.System`.

## Testing overrides

`AddKorasAI` uses `TryAddSingleton` for `IChatClient`, `IEmbeddingClient`, and
`IChatClientFactory`, so a registration made **before** it wins. In integration tests,
replace the whole AI layer with a fake:

```csharp
var fake = new FakeChatClient().EnqueueResponse("stubbed answer");

using var factory = new WebApplicationFactory<Program>()
    .WithWebHostBuilder(host => host.ConfigureServices(services =>
    {
        services.AddSingleton<IChatClient>(fake);   // wins over AddKorasAI's TryAdd
    }));
```

Alternatively, register a fake as a named client inside `AddKorasAI` — useful when code
under test resolves through `IChatClientFactory`:

```csharp
services.AddKorasAI(ai =>
{
    ai.AddClient("fake", _ => new FakeChatClient().EnqueueResponse("ok")).AsDefault();
});
```

`AddClient` is the same generic registration hook provider packages use — it also serves
for wiring a fully custom provider. A minimal fake implementation is shown in the
[testing guide](testing.md).
