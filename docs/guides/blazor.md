# Blazor Guide

Streaming an AI response into a Blazor Server UI, token by token. There is no dedicated
Blazor sample in the repository yet — this is a pattern guide built on the same APIs as the
other samples; the registration side is identical to any ASP.NET Core app.

## Registration

In `Program.cs`, exactly as in the [ASP.NET Core guide](aspnet-core.md):

```csharp
builder.Services.AddKorasAI(ai =>
{
    ai.AddOllama(builder.Configuration.GetSection("Koras:AI:Ollama"));
    ai.UseRetry();
});
```

`IChatClient` is a thread-safe singleton, so injecting it into components is safe — the
care in Blazor goes into *rendering*, not the client.

## A streaming chat component

```razor
@implements IDisposable
@inject IChatClient Chat

<textarea @bind="_prompt" rows="3"></textarea>
<button @onclick="SendAsync" disabled="@_busy">Send</button>

<pre>@_answer</pre>
@if (_error is not null)
{
    <p class="error">@_error</p>
}

@code {
    private string _prompt = "";
    private string _answer = "";
    private string? _error;
    private bool _busy;
    private CancellationTokenSource? _cts;

    private async Task SendAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _answer = "";
        _error = null;
        _busy = true;

        try
        {
            await foreach (ChatStreamUpdate update in Chat.StreamAsync(
                ChatRequest.FromPrompt(_prompt), _cts.Token))
            {
                if (update.TextDelta is { Length: > 0 } delta)
                {
                    _answer += delta;
                    await InvokeAsync(StateHasChanged);   // marshal to the renderer
                }
            }
        }
        catch (OperationCanceledException)
        {
            // User navigated away or sent a new prompt — nothing to report.
        }
        catch (AiException ex)
        {
            _error = $"AI error ({ex.Code}): {ex.Message}";
        }
        finally
        {
            _busy = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();     // component removed → stop the stream, release the connection
        _cts?.Dispose();
    }
}
```

## Why each piece matters

**`InvokeAsync(StateHasChanged)` per delta.** Stream updates arrive on background
continuations, not the component's sync context. `InvokeAsync` marshals the re-render onto
the renderer's dispatcher — calling bare `StateHasChanged()` from the streaming loop throws
or corrupts state under load. This is the standard Blazor rule applied to
`IAsyncEnumerable` consumption.

**Cancel on `Dispose`.** When the user navigates away, Blazor disposes the component. Cancelling
the token ends the `await foreach`, which disposes the stream enumerator and releases the
provider connection — otherwise the model keeps generating (and billing) for a UI that no
longer exists ([lifecycle](../concepts/lifecycle.md)).

**Cancel the previous request on re-send.** The `_cts?.Cancel()` at the top of `SendAsync`
prevents two overlapping streams appending to `_answer` interleaved.

**`OperationCanceledException` is not an error.** It is the expected result of both patterns
above, so it is swallowed; real failures arrive as `AiException`
([error handling](../concepts/error-handling.md)).

## Throttling renders (optional)

For fast models, re-rendering per token can be wasteful. Batch deltas and render on a small
interval:

```csharp
var pending = new System.Text.StringBuilder();
var lastRender = Environment.TickCount64;

await foreach (ChatStreamUpdate update in Chat.StreamAsync(request, _cts.Token))
{
    pending.Append(update.TextDelta);
    if (Environment.TickCount64 - lastRender > 50)     // ~20 fps
    {
        _answer += pending.ToString();
        pending.Clear();
        lastRender = Environment.TickCount64;
        await InvokeAsync(StateHasChanged);
    }
}

_answer += pending.ToString();
await InvokeAsync(StateHasChanged);
```

## Blazor WebAssembly

WASM apps cannot hold provider API keys in the browser. Host the AI call server-side —
the SSE endpoint from the [minimal API guide](minimal-api.md) is exactly the right shape —
and stream to the WASM client over HTTP.
