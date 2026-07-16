# Console Sample

Demonstrates the five core Koras.AI operations from a console app: chat, streaming, structured
output, automatic tool calling, and embeddings.

## Prerequisites

Default (no API key): [Ollama](https://ollama.com) running locally with the models pulled:

```bash
ollama pull llama3.2
ollama pull nomic-embed-text
```

Optional (hosted): an OpenAI API key via user secrets — never put keys in code or committed files:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."   # from this directory
```

When the key is present, the sample switches its default client to OpenAI (`.AsDefault()`).

## Run

```bash
dotnet run
```

## Expected output

Five sections (`== Chat ==`, `== Streaming ==`, `== Structured output ==`, `== Tool calling ==`,
`== Embeddings ==`) with model output and token usage. Exact text varies by model.

## Error scenarios

- Ollama not running → `Network error … Is Ollama running?` with an install tip (exit code 1).
- Invalid OpenAI key → `AI error [Authentication] from openai …`.
- Ctrl-C mid-stream → cancellation propagates and the app exits with code 130.

## Docs

- [Getting started](../../docs/getting-started/quick-start.md)
- [Tool calling](../../docs/features/tool-calling.md)
- [Structured output](../../docs/features/structured-output.md)

To use released packages instead of project references, replace the `<ProjectReference>` items
with `<PackageReference Include="Koras.AI.Ollama" />` etc. — see
[installation](../../docs/getting-started/installation.md).
