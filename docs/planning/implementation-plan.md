# Implementation Plan

Strategy: build inward-out along the dependency direction — abstractions first, then core
composition, then providers against recorded wire fixtures, then integrations, samples, and
hardening. Every milestone ends green (`build` + `test`) and committed.

## Milestone map

| # | Milestone | Contents | Exit criteria |
|---|---|---|---|
| 0 | Repository foundation | solution, Directory.Build.props, CPM, editorconfig, CLAUDE.md, CI skeleton, community files | `dotnet build` of empty solution green in CI |
| 1 | Abstractions & core models | Koras.AI.Abstractions complete: clients, messages, options, responses, streaming updates, tools, formats, errors, embeddings | unit tests for models/serialization green |
| 2 | Core implementation | decorator base, retry, structured output, tool loop, prompt templates, provider plumbing (HTTP/SSE/JSONL/errors), telemetry & logging decorators | unit tests incl. FakeTimeProvider retry suite green |
| 3 | DI & configuration | AddKorasAI, KorasAiBuilder, factory, options validation, fallback client, config binding | DI test suite green; two-provider composition test |
| 4 | Provider packages | OpenAI → Azure OpenAI → Anthropic → Ollama → Gemini, each with fixture-driven mapping tests + shared contract suite | contract tests green ×5 |
| 5 | ASP.NET Core integration | health checks package + probes per provider | health-check tests green |
| 6 | Observability polish | OTel package, metric/activity assertions, GenAI conventions review | telemetry tests green |
| 7 | Samples & documentation | 4 samples, full docs tree, README | samples build & run; docs complete |
| 8 | Hardening | integration tests (in-process fake servers incl. SSE), architecture tests, security checklist, benchmarks project | full suite green; `dotnet format` clean |
| 9 | Packaging & release readiness | pack metadata, icon, README-in-package, SourceLink, symbols, package validation, consumer smoke project, release workflows | `dotnet pack` artifacts validated |

## Recommended MVP (summary)

All F-001..F-019 (see [mvp-scope](../features/mvp-scope.md)).

## Packages to create

`Koras.AI.Abstractions`, `Koras.AI`, `Koras.AI.OpenAI`, `Koras.AI.AzureOpenAI`,
`Koras.AI.Anthropic`, `Koras.AI.Gemini`, `Koras.AI.Ollama`, `Koras.AI.AspNetCore`,
`Koras.AI.OpenTelemetry`.

## First five implementation tasks

1. **T-000** Repository foundation (build props, CPM, solution, analyzers, CI skeleton).
2. **T-101** Message/request/response/usage models + `ChatRole`/`ChatFinishReason` +
   serialization tests.
3. **T-102** `AiException`/`AiErrorCode` + transient classification + tests.
4. **T-103** `IChatClient`/`IEmbeddingClient`/`ChatStreamUpdate` + `DelegatingChatClient`.
5. **T-104** Tool models (`AiTool.Create` schema generation, `ToolCall.ParseArguments`) + tests.

## Highest risks (full register: [risks](risks.md))

R-01 wire-format drift, R-02 M.E.AI competition, R-03 premature API lock-in, R-04 streaming
edge cases, R-05 scope creep.

## Deferred features

F-020..F-029 (multimodal, memory, M.E.AI bridge, cost, RAG, caching, load balancing, agents,
moderation, public middleware) — see [future-roadmap](../features/future-roadmap.md).
