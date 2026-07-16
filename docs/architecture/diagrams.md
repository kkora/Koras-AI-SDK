# Architecture Diagrams

## Package dependency diagram

```mermaid
graph TD
    App[Application] --> ASP[Koras.AI.AspNetCore]
    App --> OTEL[Koras.AI.OpenTelemetry]
    App --> OAI[Koras.AI.OpenAI]
    App --> AZ[Koras.AI.AzureOpenAI]
    App --> ANT[Koras.AI.Anthropic]
    App --> GEM[Koras.AI.Gemini]
    App --> OLL[Koras.AI.Ollama]
    ASP --> CORE[Koras.AI]
    OTEL --> CORE
    OAI --> CORE
    AZ --> OAI
    ANT --> CORE
    GEM --> CORE
    OLL --> CORE
    CORE --> ABS[Koras.AI.Abstractions]
    Lib[Consumer libraries] --> ABS
```

## Component architecture

```mermaid
graph LR
    subgraph Koras.AI
        B[KorasAiBuilder / AddKorasAI] --> F[IChatClientFactory]
        F --> D1[TelemetryChatClient]
        D1 --> D2[LoggingChatClient]
        D2 --> D3[ToolInvokingChatClient*]
        D3 --> D4[RetryChatClient]
        D4 --> D5[FallbackChatClient*]
    end
    D5 --> P[Provider client &#40;IChatClient&#41;]
    P --> H[HttpClient via IHttpClientFactory]
    style D3 stroke-dasharray: 5 5
    style D5 stroke-dasharray: 5 5
```
\* dashed decorators are opt-in.

## Request lifecycle (chat)

```mermaid
sequenceDiagram
    participant App
    participant Pipeline as Decorators
    participant Provider as ProviderChatClient
    participant API as Provider REST API
    App->>Pipeline: CompleteAsync(request, ct)
    Pipeline->>Pipeline: start Activity, log start
    Pipeline->>Provider: CompleteAsync
    Provider->>API: POST /chat (JSON, auth header)
    alt success
        API-->>Provider: 200 JSON
        Provider-->>Pipeline: ChatResponse (usage, finishReason)
        Pipeline->>Pipeline: record tokens + duration
        Pipeline-->>App: ChatResponse
    else transient error
        API-->>Provider: 429 + Retry-After
        Provider-->>Pipeline: AiException(RateLimited, IsTransient)
        Pipeline->>Pipeline: retry after delay (≤ max attempts)
        Pipeline->>Provider: CompleteAsync (attempt n+1)
    else terminal error
        API-->>Provider: 401
        Provider-->>Pipeline: AiException(Authentication)
        Pipeline-->>App: throw (span status = error)
    end
```

## Provider lifecycle (registration → resolution)

```mermaid
sequenceDiagram
    participant Startup as Program.cs
    participant Builder as KorasAiBuilder
    participant DI as ServiceProvider
    participant Factory as IChatClientFactory
    Startup->>Builder: AddKorasAI(ai => ai.AddOpenAI(...).AddOllama(...))
    Builder->>DI: register options (+ValidateOnStart), named HttpClients, registrations
    Note over DI: startup — options validated, fail fast
    DI->>Factory: first resolution
    Factory->>Factory: build provider client + decorator chain, cache per name
    Factory-->>DI: IChatClient (default name)
```

## Error lifecycle

```mermaid
flowchart LR
    W[Wire error / exception] --> M[Provider error mapper]
    M --> E["AiException {Code, IsTransient, RetryAfter}"]
    E --> R{IsTransient?}
    R -- yes --> RT[Retry ≤ N, honor RetryAfter] --> P2{Still failing?}
    R -- no --> FB
    P2 -- yes --> FB{Fallback configured & eligible?}
    P2 -- no --> OK[Response]
    FB -- yes --> NEXT[Next candidate client]
    FB -- no --> T[Telemetry: error.type tag] --> C[Caller catches AiException]
```

## Dependency-injection flow

```mermaid
flowchart TD
    A[services.AddKorasAI] --> B[KorasAiBuilder]
    B --> C[AddOpenAI 'openai' + options + HttpClient]
    B --> D[AddOllama 'local' + options + HttpClient]
    B --> E[AddFallback 'resilient' → openai, local]
    B --> F[UseRetry global decorator]
    C & D & E --> G[ChatClientRegistration list]
    G --> H[IChatClientFactory singleton]
    H --> I[IChatClient default]
    H --> J[IEmbeddingClient default]
```

## Telemetry flow

```mermaid
flowchart LR
    OP[Chat/Embedding operation] --> ACT[ActivitySource 'Koras.AI']
    OP --> MET[Meter 'Koras.AI']
    OP --> LOG[ILogger 'Koras.AI.*']
    ACT --> OTELT[OpenTelemetry TracerProvider via AddKorasAI]
    MET --> OTELM[MeterProvider via AddKorasAI]
    LOG --> HOST[Host logging pipeline]
    OTELT & OTELM --> EXP[OTLP exporter → APM]
```
