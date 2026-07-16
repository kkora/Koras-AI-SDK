# ADR-0008: Bounded in-package retry instead of Polly / Microsoft.Extensions.Resilience

**Status:** Accepted · **Date:** 2026-07-16

## Context
Retry needs: bounded attempts, exponential backoff with full jitter, `Retry-After` honoring,
per-attempt timeout, transient-only classification, `TimeProvider` testability. Polly v8 /
Microsoft.Extensions.Resilience deliver this but operate naturally at the `HttpClient` handler
level — *below* our error-normalization boundary — and add a dependency whose version policy we
would inherit. AI-specific semantics (retry only before first streamed token; `Retry-After`
from JSON bodies, not only headers) need custom code either way.

## Decision
Implement `RetryChatClient` (~150 LOC) in core: full-jitter exponential backoff, honors
`AiException.RetryAfter`, per-attempt timeout via linked CTS, `TimeProvider`-driven delays,
retries only `IsTransient` errors, streaming retries only before the first emitted update.
Consumers who prefer Polly can wrap clients themselves (decorator extension point) or add
resilience handlers to the named `HttpClient`.

## Consequences
- No new dependency; retry semantics precisely fit the error model.
- We own ~150 LOC of well-tested backoff code — covered by `FakeTimeProvider` unit tests.
