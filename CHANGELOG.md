# Changelog

All notable changes to this project are documented in this file.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0-preview.1] — 2026-07-16

Initial preview.

### Added

- `Koras.AI.Abstractions`: `IChatClient`, `IEmbeddingClient`, `IProviderHealthProbe`, chat
  message/request/response models, streaming updates, tool models with delegate-based schema
  generation, response formats (text/JSON/JSON-schema/typed), embeddings models,
  `AiException` + `AiErrorCode` normalized error taxonomy.
- `Koras.AI` core: DI builder (`AddKorasAI`), named clients + `IChatClientFactory`,
  decorator pipeline (`DelegatingChatClient`), retry with exponential backoff + jitter +
  `Retry-After`, provider fallback, tool-invocation loop, structured output
  (`CompleteAsync<T>`), prompt templates, logging, `ActivitySource`/`Meter` telemetry
  following OTel GenAI semantic conventions, provider plumbing (`ProviderChatClient`,
  SSE/JSON-lines readers).
- Providers: `Koras.AI.OpenAI`, `Koras.AI.AzureOpenAI`, `Koras.AI.Anthropic`,
  `Koras.AI.Gemini`, `Koras.AI.Ollama` — chat, streaming, tools, structured output, and
  embeddings per the provider capability matrix.
- Integrations: `Koras.AI.AspNetCore` (health checks), `Koras.AI.OpenTelemetry`
  (tracer/meter registration).
- Samples: Console, Web API, Minimal API, Worker Service.
- Full documentation tree under `docs/`.

