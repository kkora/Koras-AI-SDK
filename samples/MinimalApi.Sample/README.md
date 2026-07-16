# Minimal API Sample

A two-endpoint minimal API: `/chat` (JSON) and `/chat/stream` (server-sent events).

## Setup

Default backend is local [Ollama](https://ollama.com) (`ollama pull llama3.2`). To use OpenAI
instead, add the key via user secrets (never in appsettings.json):

```bash
dotnet user-secrets set "Koras:AI:OpenAI:ApiKey" "sk-..."
```

## Run

```bash
dotnet run
curl -s localhost:5000/chat -H 'content-type: application/json' -d '{"prompt":"hello"}'
curl -N localhost:5000/chat/stream -H 'content-type: application/json' -d '{"prompt":"count to 5"}'
```

## Expected output

`/chat` returns `{ "text": ..., "provider": ..., "inputTokens": ..., "outputTokens": ... }`;
`/chat/stream` emits `data: "..."` SSE frames ending with `data: [DONE]`.

## Error scenarios

Ollama down → HTTP 500 with an `AiException` (`Network`) in logs suggesting `ollama serve`.
Disconnecting the client mid-stream cancels the provider call via `CancellationToken`.

Docs: [Minimal API guide](../../docs/guides/minimal-api.md), [Streaming](../../docs/features/streaming.md).
