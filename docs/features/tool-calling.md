# Tool Calling

## Overview

Tools (function calling) let the model request that your code run and use the result to finish
its answer. Koras.AI models a tool as `AiTool`:

- `AiTool.Create(name, description, handler)` — an executable tool; the JSON Schema for its
  arguments is generated from the delegate's parameters.
- `AiTool.Declare(name, description, parametersSchema)` — a declaration-only tool; the model
  can call it, but your application dispatches the call itself.

With `ai.UseToolInvocation()` the SDK runs the loop automatically: it executes requested
handlers, appends `ChatMessage.ToolResult(...)` messages, and re-calls the model until it
produces a final answer (bounded by `ToolInvocationOptions.MaxIterations`, default 8).

## When to use it

Use tools to ground answers in live data (weather, database lookups, calculators) or to let
the model take actions. Use declaration-only tools when execution must go through your own
dispatch, authorization, or queueing logic.

## Required packages

- `Koras.AI` plus a provider package. All five providers support tool calling (Ollama:
  model-dependent).

## Basic usage

```csharp
using System.ComponentModel;
using Koras.AI;

AiTool weather = AiTool.Create(
    "get_weather",
    "Gets the current weather for a city.",
    ([Description("City name, e.g. 'Paris'")] string city)
        => $"{{\"city\":\"{city}\",\"tempC\":21}}");

var request = new ChatRequest
{
    Messages = [ChatMessage.User("What's the weather in Paris?")],
    Options = new ChatOptions { Tools = [weather] },
};

ChatResponse response = await client.CompleteAsync(request, ct);
Console.WriteLine(response.Text); // final answer, tools already executed by the loop
```

Handlers may be sync or async (`Task<T>`/`ValueTask<T>`), return `string` or any serializable
value, and may declare a trailing `CancellationToken` parameter that receives the caller's token.

`ChatOptions.ToolChoice` steers usage: `ToolChoice.Auto` (default when tools are present),
`ToolChoice.None`, `ToolChoice.Required`, or `ToolChoice.Tool("get_weather")`.

## Dependency-injection usage

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOpenAI(o =>
    {
        o.ApiKey = builder.Configuration["OpenAI:ApiKey"];
        o.DefaultModel = "gpt-4o-mini";
    });
    ai.UseToolInvocation(t =>
    {
        t.MaxIterations = 8;
        t.ErrorBehavior = ToolErrorBehavior.ReturnToModel;
    });
});
```

Without `UseToolInvocation`, responses come back with `FinishReason == ChatFinishReason.ToolCalls`
and the calls on `response.Message.ToolCalls`; handle them manually:

```csharp
foreach (ToolCall call in response.Message.ToolCalls)
{
    var args = call.ParseArguments<WeatherArgs>();      // throws AiException(InvalidResponse) on bad JSON
    string result = await Dispatch(call.Name, args, ct);
    messages.Add(ChatMessage.ToolResult(call.Id, result));
}
```

## Execution lifecycle

The loop applies to `CompleteAsync` only. Each iteration: model call → if the response's tool
calls all reference invocable tools, execute them (each in an `execute_tool {tool}` activity),
append results, repeat. Responses that call declaration-only tools are returned untouched.
Streaming passes through so callers observe raw `ToolCallDelta` updates — see [streaming](streaming.md).
Token usage across all iterations is summed into the final `ChatResponse.Usage`.

## Error handling

- Handler throws with `ToolErrorBehavior.ReturnToModel` (default): the failure is serialized
  as the tool result (`{"error":"Tool 'x' failed: ..."}`) and the model may recover.
- Handler throws with `ToolErrorBehavior.Throw`: `AiException` with
  `AiErrorCode.ToolExecutionFailed` propagates, original exception as `InnerException`.
- Loop exceeds `MaxIterations`: `AiException` with `AiErrorCode.ToolExecutionFailed`.
- Model sends malformed/missing arguments: `AiException` with `AiErrorCode.InvalidResponse`.

See [error handling](error-handling.md).

## Cancellation

The caller's token flows into each model call and into handlers that declare a
`CancellationToken` parameter. Cancellation propagates as `OperationCanceledException`.

## Telemetry

Each handler execution creates an `execute_tool {tool}` activity tagged with
`gen_ai.tool.name` and `gen_ai.tool.call.id`. See [telemetry](telemetry.md).

## Security considerations

Tool handlers are an execution surface driven by model output — treat arguments as untrusted
input. Validate and authorize inside the handler; never build shell commands or SQL from raw
argument strings. Prefer declaration-only tools when a human or policy check must approve the
action first. Use lower_snake_case tool names for widest provider compatibility.

## Thread safety

`AiTool` instances are immutable and safe to share across requests and threads; handlers must
be thread-safe themselves if they touch shared state.

## Testing applications using this feature

Tools are directly invocable, so unit-test them without a model:

```csharp
AiTool tool = AiTool.Create("add", "Adds two numbers.", (int a, int b) => a + b);
string result = await tool.InvokeAsync("""{"a":2,"b":3}""");
Assert.Equal("5", result);
```

For loop behavior, wrap a scripted fake `IChatClient` with
`new ToolInvokingChatClient(fake, new ToolInvocationOptions())` and assert on the message
sequence it sends.

## Common mistakes

- Registering tools but forgetting `ai.UseToolInvocation()` and then wondering why
  `FinishReason` is `ToolCalls` and `Text` is null.
- Expecting the loop to run during streaming — it applies to `CompleteAsync` only.
- Losing `ToolCall.Id` when replying manually; `ChatMessage.ToolResult` must reference it
  (Gemini and Ollama ids are synthesized as `call_{index}_{name}` — round-trip them verbatim).
- Unbounded tools (for example a tool the model can call forever); tune `MaxIterations`.

## Related features

- [Chat completion](chat-completion.md)
- [Streaming](streaming.md)
- [Structured output](structured-output.md)
- [Error handling](error-handling.md)
