# Feature Matrix

## Feature → package → deliverables

| Feature | Core Package | Integration Package | MVP | Tests | Documentation | Example |
|---|---|---|---|---|---|---|
| F-001 Chat completion | Abstractions + Koras.AI | provider packages | ✅ | unit+integration+contract | features/chat-completion.md | all samples |
| F-002 Streaming | Abstractions + Koras.AI | provider packages | ✅ | unit+integration | features/streaming.md | Console, MinimalApi |
| F-003 Structured output | Koras.AI | provider packages | ✅ | unit+integration | features/structured-output.md | Console, WebApi |
| F-004 Tool calling | Abstractions + Koras.AI | provider packages | ✅ | unit+integration | features/tool-calling.md | Console |
| F-005 Embeddings | Abstractions | OpenAI, AzureOpenAI, Gemini, Ollama | ✅ | unit+contract | features/embeddings.md | Console |
| F-006 Prompt templates | Koras.AI | — | ✅ | unit | features/prompt-templates.md | Console |
| F-007 OpenAI provider | — | Koras.AI.OpenAI | ✅ | unit+integration+contract | features/provider-openai.md | all samples |
| F-008 Azure OpenAI provider | — | Koras.AI.AzureOpenAI | ✅ | unit+contract | features/provider-azure-openai.md | WebApi |
| F-009 Anthropic provider | — | Koras.AI.Anthropic | ✅ | unit+integration+contract | features/provider-anthropic.md | Console |
| F-010 Gemini provider | — | Koras.AI.Gemini | ✅ | unit+contract | features/provider-gemini.md | — |
| F-011 Ollama provider | — | Koras.AI.Ollama | ✅ | unit+contract | features/provider-ollama.md | Console (default) |
| F-012 DI & options | Koras.AI | AspNetCore | ✅ | unit | features/dependency-injection.md | all samples |
| F-013 Retry & timeout | Koras.AI | — | ✅ | unit | features/resilience.md | WorkerService |
| F-014 Provider fallback | Koras.AI | — | ✅ | unit | features/provider-fallback.md | WebApi |
| F-015 Error normalization | Abstractions + Koras.AI | provider packages | ✅ | unit (matrix) | features/error-handling.md | all samples |
| F-016 Logging | Koras.AI | — | ✅ | unit | guides/logging.md | WorkerService |
| F-017 Telemetry & usage | Koras.AI | Koras.AI.OpenTelemetry | ✅ | unit | features/telemetry.md | WebApi |
| F-018 Health checks | — | Koras.AI.AspNetCore | ✅ | unit | features/health-checks.md | WebApi |
| F-019 Custom providers | Abstractions + Koras.AI | — | ✅ | contract | features/custom-providers.md | docs example |

## Provider capability matrix (MVP)

| Capability | OpenAI | Azure OpenAI | Anthropic | Gemini | Ollama |
|---|---|---|---|---|---|
| Chat | ✅ | ✅ | ✅ | ✅ | ✅ |
| Streaming | ✅ SSE | ✅ SSE | ✅ SSE | ✅ SSE | ✅ JSONL |
| Tool calling | ✅ | ✅ | ✅ | ✅ | ✅ (model-dependent) |
| Structured output | ✅ json_schema | ✅ json_schema | ✅ via tool | ✅ responseSchema | ✅ format |
| Embeddings | ✅ | ✅ | ❌ `NotSupported` | ✅ | ✅ |
| Health probe | ✅ /models | ✅ deployment GET | ✅ cheap /models | ✅ models.get | ✅ /api/version |
| Retry-After surfaced | ✅ | ✅ | ✅ | ✅ | n/a |

Unsupported capability calls throw `AiException` with `AiErrorCode.NotSupported` — never a
silent degradation.
