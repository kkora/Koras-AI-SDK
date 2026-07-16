# ADR-0006: Decorator pipeline instead of a public middleware API

**Status:** Accepted · **Date:** 2026-07-16

## Context
Cross-cutting behavior (retry, fallback, telemetry, tool loop, logging) needs a composition
model. Options: an ASP.NET-style middleware pipeline (`Use(Func<Context, Next, Task>)`) or
plain decorators over `IChatClient`.

## Decision
Decorators: an abstract `DelegatingChatClient` (mirroring `DelegatingHandler`) plus builder
hooks `ai.Use(Func<IChatClient, IChatClient>)` (global) and per-client variants. All built-in
cross-cutting features are decorators, proving the model.

## Consequences
- Smaller API: no context object, no pipeline ordering DSL; ordering is documented and fixed
  for built-ins (Telemetry → Logging → ToolLoop → Retry → Fallback → Provider).
- Both request/response *and* streaming compose naturally (`IAsyncEnumerable` pass-through).
- A public middleware API remains possible later (F-029) without breaking decorators.
