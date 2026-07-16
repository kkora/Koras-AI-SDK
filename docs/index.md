# Koras.AI Documentation

Koras.AI is a provider-neutral .NET SDK for building AI-powered applications. One set of
abstractions — `IChatClient`, `IEmbeddingClient`, `ChatRequest`, `AiException` — works
identically across OpenAI, Azure OpenAI, Anthropic, Google Gemini, and Ollama, with
production plumbing built in: dependency injection with startup-validated options, retry with
exponential backoff, provider fallback, a normalized error taxonomy, structured output,
tool calling, streaming, health checks, and OpenTelemetry-convention telemetry. Swap
providers by changing configuration, not code.

## Getting started

- [Getting started](getting-started/) — installation, first request, choosing a provider.

## Concepts

- [Concepts](concepts/) — clients, named registrations, the decorator pipeline, requests and
  responses, the error model.

## Features

| Area | Pages |
|---|---|
| Core operations | [Chat completion](features/chat-completion.md) · [Streaming](features/streaming.md) · [Structured output](features/structured-output.md) · [Tool calling](features/tool-calling.md) · [Embeddings](features/embeddings.md) · [Prompt templates](features/prompt-templates.md) |
| Providers | [OpenAI](features/provider-openai.md) · [Azure OpenAI](features/provider-azure-openai.md) · [Anthropic](features/provider-anthropic.md) · [Gemini](features/provider-gemini.md) · [Ollama](features/provider-ollama.md) |
| Composition & resilience | [Dependency injection](features/dependency-injection.md) · [Retry & timeout](features/resilience.md) · [Provider fallback](features/provider-fallback.md) · [Error handling](features/error-handling.md) |
| Operations | [Telemetry](features/telemetry.md) · [Health checks](features/health-checks.md) · [Custom providers](features/custom-providers.md) |

The [feature catalog](features/feature-catalog.md) and
[feature matrix](features/feature-matrix.md) map features to packages and provider
capabilities; the [future roadmap](features/future-roadmap.md) covers post-1.0 plans.

## Guides

- [Guides](guides/) — task-oriented walkthroughs, including [logging](guides/logging.md).

## Recipes

- [Common scenarios](recipes/common-scenarios.md) — copy-paste snippets for everyday tasks.
- [Advanced scenarios](recipes/advanced-scenarios.md) — multi-client, decorators, escape hatches.
- [Production configuration](recipes/production-configuration.md) — the recommended prod setup.
- [Testing recipes](recipes/testing-recipes.md) — fakes, scripted responses, integration tests.

## Configuration

- [All options reference](configuration/all-options.md) — every option, default, and validation rule.
- [appsettings.json](configuration/appsettings.md) — configuration sections and per-environment overrides.
- [Environment variables](configuration/environment-variables.md) — `Koras__AI__*` mapping, containers, Kubernetes.
- [Startup validation](configuration/validation.md) — what `ValidateOnStart` checks and how failures look.

## Troubleshooting

- [Common errors](troubleshooting/common-errors.md) — every `AiErrorCode`, causes, and fixes.
- [Diagnostics](troubleshooting/diagnostics.md) — debug logging, request ids, tracing, health probes.
- [Logging](troubleshooting/logging.md) — categories, levels, event ids, sensitive-data switch.
- [Provider errors](troubleshooting/provider-errors.md) — per-provider quirks and their mappings.
- [FAQ](troubleshooting/faq.md) — frequently asked questions.

## Migration & versioning

- [Versioning policy](migration/versioning-policy.md) — SemVer rules, TFM support, deprecation.
- [Upgrading](migration/upgrading.md) — how upgrades work between releases.
- [Breaking changes](migration/breaking-changes.md) — the log (empty pre-1.0) and entry template.

## Architecture

- [Overview](architecture/overview.md) · [Package boundaries](architecture/package-boundaries.md) ·
  [Dependency rules](architecture/dependency-rules.md) · [Extension model](architecture/extension-model.md)
- [Error model](architecture/error-model.md) · [Observability](architecture/observability.md) ·
  [Diagrams](architecture/diagrams.md)
- [Decision records](architecture/decision-records/) — ADR-0001 through ADR-0009.

## Security, performance, testing, release

- [Security](security/) — threat model, secret handling, transport rules.
- [Performance](performance/) — allocation and streaming characteristics, benchmarks.
- [Testing](testing/) — the test strategy behind the SDK and how to test your own code.
- [Release](release/) — release process, signing, package validation.

## API reference

- [API reference overview](api-reference/overview.md) — public namespaces and types per package.
  XML documentation ships in every package; IntelliSense is the primary day-to-day reference.

## Project meta

- [Product docs](product/) — vision, positioning, personas, use cases.
- [Planning docs](planning/) — milestones, backlog, risks, definition of done.
- [API design docs](api/) — [public API design](api/public-api-design.md),
  [backward compatibility](api/backward-compatibility.md),
  [naming guidelines](api/naming-guidelines.md).
