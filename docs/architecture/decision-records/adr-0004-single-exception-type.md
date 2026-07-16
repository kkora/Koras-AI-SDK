# ADR-0004: Single `AiException` with `AiErrorCode` taxonomy

**Status:** Accepted · **Date:** 2026-07-16

## Context
Alternatives: an exception hierarchy (`AiRateLimitException : AiException`…) or a Result type.
Hierarchies grow into breaking changes (new subclass = new catch semantics) and invite
provider-specific leaf types. Result types fight the platform (HttpClient, streaming
enumerators throw anyway).

## Decision
One sealed-ish exception `AiException` carrying `AiErrorCode`, `Provider`, `StatusCode`,
`RetryAfter`, `IsTransient`, `ProviderErrorBody`, `RequestId`. Cancellation keeps the standard
`OperationCanceledException` contract.

## Consequences
- Consumers write `catch (AiException ex) when (ex.Code == AiErrorCode.RateLimited)`.
- Adding a code is additive (enum value) — documented as non-breaking; consumers must
  default-case their switches.
- Retry/fallback decorators depend only on `IsTransient`, keeping custom providers first-class.
