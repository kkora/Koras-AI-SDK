# Problem Statement

## The problem

.NET developers integrating generative AI face a fragmented provider landscape. Each provider
ships its own SDK (or none), its own request/response models, its own streaming wire format, its
own error semantics, and its own rate-limiting behavior:

| Concern | OpenAI | Azure OpenAI | Anthropic | Gemini | Ollama |
|---|---|---|---|---|---|
| Auth | `Authorization: Bearer` | `api-key` header / Entra ID | `x-api-key` + version header | `x-goog-api-key` | none (local) |
| Chat shape | `messages[]` incl. system | same, per-deployment URL | `system` separate from `messages[]` | `contents[]` + `systemInstruction` | `messages[]` |
| Streaming | SSE `data:` + `[DONE]` | SSE | SSE typed events | SSE (`alt=sse`) | JSON lines |
| Tool schema | `tools[].function.parameters` | same | `tools[].input_schema` | `functionDeclarations[]` | `tools[]` |
| Rate limit | 429 + `Retry-After` | 429 + `Retry-After` | 429/529 | 429 | n/a |

Application code written against any one of these is rewritten for the next one. Teams that need
provider redundancy (regulatory, cost, or availability reasons) pay the cost several times over,
then write a home-grown facade — untested, undocumented, unowned.

## Who feels the pain

- **ASP.NET Core / SaaS teams** shipping AI features who must not be locked to one vendor's
  pricing or availability.
- **Enterprise teams** required to run Azure OpenAI in production but wanting OpenAI or local
  Ollama in development.
- **AI engineers** who prototype against Ollama locally and deploy against hosted providers.
- **Library authors** who need to accept "any chat model" without dictating a vendor.

## Cost of the status quo

- Duplicate integration code per provider (~1–3k LOC each, plus tests).
- Inconsistent resilience: some paths retry, some don't; `Retry-After` frequently ignored.
- No unified telemetry: token spend and latency invisible per provider.
- Vendor lock-in as an accident of the first SDK chosen, not a decision.

## The solution

A small, layered SDK:

- `Koras.AI.Abstractions` — the contract every application codes against.
- `Koras.AI` — pipeline, resilience, fallback, structured output, templates, DI, telemetry.
- One focused package per provider, each a thin REST adapter that normalizes requests,
  responses, streaming, and errors into the shared model.

## What we explicitly do not solve

- Prompt quality, evaluation, and guardrail authoring.
- Vector storage and retrieval infrastructure (RAG *abstractions* are roadmap, storage is not).
- Fine-tuning, batch jobs, file management, and other provider control-plane APIs (v2+ candidates).
