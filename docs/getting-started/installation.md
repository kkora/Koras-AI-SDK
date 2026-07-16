# Installation

Koras.AI ships as a set of NuGet packages targeting **net8.0, net9.0, and net10.0**. You
install the core package plus one package per AI provider you want to talk to.

## Packages at a glance

| Package | Install when you… |
|---|---|
| `Koras.AI` | build an application — brings the DI builder, retry/fallback, tool loop, structured output, prompt templates (also pulls in `Koras.AI.Abstractions`) |
| `Koras.AI.Abstractions` | build a *library* that only needs the contracts (`IChatClient`, models, `AiException`) |
| `Koras.AI.OpenAI` | call the OpenAI API (or any OpenAI-compatible gateway) |
| `Koras.AI.AzureOpenAI` | call Azure OpenAI deployments |
| `Koras.AI.Anthropic` | call Anthropic (Claude) |
| `Koras.AI.Gemini` | call Google Gemini |
| `Koras.AI.Ollama` | call a local or self-hosted Ollama server (no API key) |
| `Koras.AI.AspNetCore` | want provider health checks in an ASP.NET Core app |
| `Koras.AI.OpenTelemetry` | export Koras.AI traces and metrics through OpenTelemetry |

Provider packages depend on `Koras.AI`, so installing a provider package is enough for a
typical application.

## Preview version note

Koras.AI is currently published as a preview (`0.1.0-preview.1`). The NuGet CLI does not
resolve prerelease packages implicitly, so pass the version (or `--prerelease`) explicitly:

```sh
dotnet add package Koras.AI.OpenAI -v 0.1.0-preview.1
```

## Install per scenario

**Console app or worker calling OpenAI:**

```sh
dotnet add package Koras.AI.OpenAI -v 0.1.0-preview.1
```

**Local development with Ollama (zero API keys):**

```sh
dotnet add package Koras.AI.Ollama -v 0.1.0-preview.1
```

**ASP.NET Core API with fallback across providers, health checks, and OpenTelemetry:**

```sh
dotnet add package Koras.AI.OpenAI -v 0.1.0-preview.1
dotnet add package Koras.AI.Anthropic -v 0.1.0-preview.1
dotnet add package Koras.AI.Ollama -v 0.1.0-preview.1
dotnet add package Koras.AI.AspNetCore -v 0.1.0-preview.1
dotnet add package Koras.AI.OpenTelemetry -v 0.1.0-preview.1
```

**Class library that accepts an `IChatClient` but never constructs one:**

```sh
dotnet add package Koras.AI.Abstractions -v 0.1.0-preview.1
```

This keeps your library free of DI, HTTP, and provider dependencies — consumers choose the
provider.

## Picking provider packages

- You can install **several provider packages side by side**; each registers a named client
  and you compose them with fallback or the client factory (see
  [dependency injection](dependency-injection.md)).
- `Koras.AI.OpenAI` also covers OpenAI-compatible gateways — point `OpenAIOptions.Endpoint`
  at the gateway URL.
- `Koras.AI.Ollama` is the recommended starting point: no account, no key, runs locally.

## Running the samples from source vs. packages

The repository's `samples/` projects reference the SDK with `<ProjectReference>` so they
always build against the current source tree:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\Koras.AI.OpenAI\Koras.AI.OpenAI.csproj" />
  <ProjectReference Include="..\..\src\Koras.AI.Ollama\Koras.AI.Ollama.csproj" />
</ItemGroup>
```

To copy a sample into your own solution, replace those with package references:

```xml
<ItemGroup>
  <PackageReference Include="Koras.AI.OpenAI" Version="0.1.0-preview.1" />
  <PackageReference Include="Koras.AI.Ollama" Version="0.1.0-preview.1" />
</ItemGroup>
```

No code changes are required — the public API is identical either way.

## Next steps

- [Quick start](quick-start.md) — a working console app in five minutes.
- [Your first application](first-application.md) — a Minimal API with streaming.
